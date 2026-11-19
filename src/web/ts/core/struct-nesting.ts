// Rules for Struct Data that contains other Struct Data.
// A signal is struct-typed when nestedTypeId (preferred) or dataType names another device type.
// A flat struct spans 1 level; a struct holding a flat struct spans 2; the limit is MAX_DEPTH.
namespace GrafcetStudioStructNesting {
  type DeviceType = GrafcetStudioProject.DeviceType;
  type DeviceSignal = GrafcetStudioProject.DeviceSignal;

  export const MAX_DEPTH = 3;
  export const PRIMITIVE_TYPES = ['Bool', 'Int', 'DInt', 'UInt', 'UDInt', 'Real', 'LReal', 'Word', 'DWord', 'Byte', 'String', 'Time'];
  // Codegen looks these up by exact name, so they never nest or get nested.
  export const SYSTEM_STRUCT_NAMES = ['Unit Station', 'MacroPort', 'SystemControl'];

  export interface NestingCheck {
    ok: boolean;
    message?: string;
  }

  /** A leaf signal of a struct; nested leaves use the dotted path as id and name. */
  export interface FlatSignal extends DeviceSignal {
    path: string;
    leafName: string;
    depth: number;
  }

  export function isSystemStructName(name: string | null | undefined): boolean {
    return SYSTEM_STRUCT_NAMES.indexOf(String(name || '')) >= 0;
  }

  export function isPrimitiveType(dataType: string | null | undefined): boolean {
    return PRIMITIVE_TYPES.indexOf(String(dataType || '').trim()) >= 0;
  }

  export function findSignalType(devices: DeviceType[], sig: DeviceSignal | null | undefined): DeviceType | undefined {
    if (!sig) return undefined;
    const list = devices || [];
    const typeId = String(sig.nestedTypeId || '').trim();
    if (typeId) {
      const byId = list.find(device => !!device && device.id === typeId);
      if (byId) return byId;
    }
    const typeName = String(sig.dataType || '').trim();
    if (!typeName || isPrimitiveType(typeName)) return undefined;
    return list.find(device => !!device && device.name === typeName)
      || list.find(device => !!device && String(device.name || '').toLowerCase() === typeName.toLowerCase());
  }

  /** Types that hold a signal of the given type directly. */
  export function usagesOf(devices: DeviceType[], typeId: string): DeviceType[] {
    return (devices || []).filter(device => !!device && device.id !== typeId
      && (device.signals || []).some(sig => {
        const child = findSignalType(devices, sig);
        return !!child && child.id === typeId;
      }));
  }

  /** Levels spanned by the type and everything below it. Loops stop counting where they close. */
  export function heightOf(devices: DeviceType[], type: DeviceType, stack: string[] = []): number {
    if (stack.indexOf(type.id) >= 0) return 0;
    const next = stack.concat(type.id);
    let deepest = 0;
    (type.signals || []).forEach(sig => {
      const child = findSignalType(devices, sig);
      if (child) deepest = Math.max(deepest, heightOf(devices, child, next));
    });
    return 1 + deepest;
  }

  /** Levels from the outermost struct that contains the type down to the type itself (1 when nothing contains it). */
  export function depthAbove(devices: DeviceType[], type: DeviceType, stack: string[] = []): number {
    if (stack.indexOf(type.id) >= 0) return 0;
    const next = stack.concat(type.id);
    let deepest = 0;
    usagesOf(devices, type.id).forEach(parent => {
      deepest = Math.max(deepest, depthAbove(devices, parent, next));
    });
    return 1 + deepest;
  }

  /** Path of type names from `from` down to `targetId`, or null when `from` never reaches it. */
  function findPath(devices: DeviceType[], from: DeviceType, targetId: string, stack: string[]): string[] | null {
    for (const sig of from.signals || []) {
      const child = findSignalType(devices, sig);
      if (!child) continue;
      if (child.id === targetId) return stack.concat(child.name);
      if (stack.indexOf(child.name) >= 0) continue;
      const hit = findPath(devices, child, targetId, stack.concat(child.name));
      if (hit) return hit;
    }
    return null;
  }

  /**
   * Checks one type against the nesting rules, as it would be stored in `devices`.
   * Only chains that pass through this type are checked, so unrelated broken data does not block it.
   */
  export function checkType(devices: DeviceType[], type: DeviceType): NestingCheck {
    for (const sig of type.signals || []) {
      const child = findSignalType(devices, sig);
      if (!child) continue;
      if (isSystemStructName(type.name)) {
        return { ok: false, message: `"${type.name}" is a system struct and cannot contain other structs.` };
      }
      if (isSystemStructName(child.name)) {
        return { ok: false, message: `"${child.name}" is a system struct and cannot be used inside "${type.name}".` };
      }
      if (child.id === type.id) {
        return { ok: false, message: `"${type.name}" cannot contain itself (signal "${sig.name}").` };
      }
    }
    const loop = findPath(devices, type, type.id, [type.name]);
    if (loop) return { ok: false, message: 'Struct nesting loop: ' + loop.join(' → ') + '.' };
    const levels = depthAbove(devices, type) + heightOf(devices, type) - 1;
    if (levels > MAX_DEPTH) {
      return { ok: false, message: `Struct nesting would reach ${levels} levels through "${type.name}"; the limit is ${MAX_DEPTH}.` };
    }
    return { ok: true };
  }

  /** Whether `parent` (null for a struct not saved yet) may hold a signal of type `child`. */
  export function canContain(devices: DeviceType[], parent: DeviceType | null, parentName: string, child: DeviceType): NestingCheck {
    const parentId = parent ? parent.id : '__new_struct__';
    const probe: DeviceType = {
      id: parentId,
      name: parentName || (parent ? parent.name : 'New struct'),
      signals: (parent ? parent.signals || [] : []).concat([{ id: '__probe__', name: '__probe__', dataType: child.name, nestedTypeId: child.id }])
    };
    const list = (devices || []).filter(device => !!device && device.id !== parentId).concat([probe]);
    return checkType(list, probe);
  }

  /**
   * Leaf signals of a struct. Top-level signals keep their own id/name; nested leaves use the
   * dotted path ("Sensor.Fwd") so addresses of two members with the same leaf name do not collide.
   */
  export function flattenSignals(devices: DeviceType[], type: DeviceType | null | undefined): FlatSignal[] {
    if (!type) return [];
    const out: FlatSignal[] = [];
    const walk = (signals: DeviceSignal[], prefix: string, depth: number, stack: string[]): void => {
      (signals || []).forEach(sig => {
        if (!sig) return;
        const leafName = sig.name || sig.id || '';
        if (!leafName) return;
        const path = prefix ? prefix + '.' + leafName : leafName;
        const child = findSignalType(devices, sig);
        if (child && depth < MAX_DEPTH && stack.indexOf(child.id) < 0) {
          walk(child.signals || [], path, depth + 1, stack.concat(child.id));
          return;
        }
        if (child) console.warn('Struct nesting not expanded at', path, depth >= MAX_DEPTH ? '(depth limit)' : '(loop)');
        out.push(Object.assign({}, sig, prefix ? { id: path, name: path } : {}, { path, leafName, depth }));
      });
    };
    walk(type.signals || [], '', 1, [type.id]);
    return out;
  }

  /** Struct-typed member paths of a struct (e.g. "Sensor", "Sensor.Probe"); these never carry an address. */
  export function containerPaths(devices: DeviceType[], type: DeviceType | null | undefined): string[] {
    if (!type) return [];
    const out: string[] = [];
    const walk = (current: DeviceType, prefix: string, depth: number, stack: string[]): void => {
      (current.signals || []).forEach(sig => {
        const child = findSignalType(devices, sig);
        if (!child || depth >= MAX_DEPTH || stack.indexOf(child.id) >= 0) return;
        const path = prefix ? prefix + '.' + (sig.name || sig.id) : (sig.name || sig.id);
        out.push(path);
        walk(child, path, depth + 1, stack.concat(child.id));
      });
    };
    walk(type, '', 1, [type.id]);
    return out;
  }

  /** Prefixes inside `root` where an instance of `targetId` sits: "" for root itself, "Sensor." for a member. */
  export function instancePrefixes(devices: DeviceType[], root: DeviceType, targetId: string): string[] {
    const out: string[] = [];
    const walk = (current: DeviceType, prefix: string, depth: number, stack: string[]): void => {
      if (current.id === targetId) out.push(prefix);
      if (depth >= MAX_DEPTH) return;
      (current.signals || []).forEach(sig => {
        const child = findSignalType(devices, sig);
        if (!child || stack.indexOf(child.id) >= 0) return;
        walk(child, prefix + (sig.name || sig.id) + '.', depth + 1, stack.concat(child.id));
      });
    };
    walk(root, '', 1, [root.id]);
    return out;
  }
}
