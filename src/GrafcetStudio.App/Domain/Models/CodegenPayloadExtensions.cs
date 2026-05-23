using System.Collections.Generic;
using System.Linq;
using GrafcetStudio.Domain.Models;

namespace GrafcetStudio.Domain.Models
{
    /// <summary>
    /// Provides extension methods for <see cref="CodegenPayload"/>.
    /// </summary>
    public static class CodegenPayloadExtensions
    {
        /// <summary>
        /// Converts a <see cref="CodegenPayload"/> into a <see cref="DiagramState"/>.
        /// The conversion copies steps, transitions, variables and initializes an empty connection list.
        /// </summary>
        /// <param name="payload">The payload to convert.</param>
        /// <returns>A populated <see cref="DiagramState"/> instance.</returns>
        public static DiagramState ToDiagramState(this CodegenPayload payload)
        {
            // Ensure collections are not null to avoid NullReferenceExceptions.
            var steps = payload.Steps ?? new List<Step>();
            var transitions = payload.Transitions ?? new List<Transition>();
            var variables = payload.Variables ?? new List<DeviceVariable>();

            // The original model does not contain explicit connections; initialize empty.
            var connections = new List<Connection>();

            return new DiagramState
            {
                Steps = steps,
                Transitions = transitions,
                Connections = connections,
                Variables = variables
            };
        }
    }
}
