---
category: multi_zone
complexity: intermediate
keywords: [zone, interlock, request, grant]
---
# Multi-Zone Interlock Pattern
- SetDO zone_req, 1;
- WaitDI zone_grant, 1;
- Enter zone and execute operation.
- SetDO zone_req, 0;
