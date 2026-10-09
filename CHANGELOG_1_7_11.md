# Khitun Geo 1.7.11

- Added a point import wizard for XLSX, CSV, TSV, TXT, XYZ, PNT, DAT, ASC, JSON and GeoJSON files.
- Added worksheet selection, column mapping, coordinate order swap, data preview, append/replace mode and visible invalid-row reporting before points are changed.
- Restored import support for Khitun Geo project JSON and legacy `pointInfoArray` files.
- Added a collapsible grouped left panel, application logo and fixed import/export actions in the top bar.
- Added persistent visualization zoom, mouse-wheel zoom, middle-button pan, fit, and zoom buttons. Fit includes drawing geometry and points.
- Clarified bulk Z operations: add, subtract, or assign one absolute height to all points.
- Preserved low-memory drawing traversal and kept DWG/DXF geometry import separate from point import.
