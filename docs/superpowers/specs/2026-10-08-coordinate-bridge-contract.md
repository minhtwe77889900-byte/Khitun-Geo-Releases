# Coordinate JSON bridge, version 1

This stage connects data contracts, not the production window. Existing JavaScript mathematics and coordinate parameters stay unchanged. An embedded JS runtime is not installed yet. Node is used only by test tools, never required by the Windows application.

## Request

`KhitunCoordinateAdapter.convertJson(string)` receives one JSON object:

```json
{
  "version": 1,
  "source": {"kind": "geo", "datum": "wgs"},
  "target": {"kind": "fixedtm", "datum": "sk42", "lon0": 84, "fe": 86209.8, "fn": -6542783.5, "k": 1},
  "zone": {"auto": true, "manual": 28, "sourcePrefix": true, "targetPrefix": true},
  "points": [{"name": "001", "x": 65, "y": 84, "height": -12, "description": "Глубина"}]
}
```

- X is northing/latitude; Y is easting/longitude. No implicit axis swapping.
- Supported resolved kinds: geo, webmerc, gk6, gk3, utm, fixedtm, custom. A regional catalogue choice must first resolve to its complete fixedtm parameters; `kind: regional` is rejected.
- Datum names follow the unchanged E table, excluding bessel (which is an ellipsoid, not a datum). Optional fixedtm/custom ellipsoid names are bessel and krass; optional `towgs` contains exactly seven finite values.
- Fixed/custom definitions require finite lon0, fe, fn and positive k; lat0 defaults to zero. Longitude parameters support the catalogue's 0–360 convention (Chukotka uses values beyond 180). Parameters are not normalized or silently substituted. Nonzero custom lat0 is rejected because the existing custom projection uses zero; krass requires SK-42/SK-95 and Bessel requires explicit towgs parameters.
- UTM and Web Mercator require WGS-84. Existing northern-hemisphere and geographic-domain checks remain enforced.
- A source without a GK Y prefix requires a manual zone. Source UTM uses the explicit manual zone. Automatic target zone is chosen on the target datum, matching the existing workflow. Required manual zones are validated before identity conversion and empty batches. Source prefix zones are validated even for identity conversion.
- Point name and description are strings; X/Y must be finite numbers. Height must be finite or null. Null height uses zero only inside horizontal calculation, and is returned as null.
- All rows are processed in memory; any invalid row aborts the request. Input data stays unchanged.

## Response and state application

Response contains `version: 1`, converted `points` in original order, and sorted unique `targetZones`. Only X and Y change. Height, name and description are preserved exactly.

`CoordinateBridge.BuildRequest` serializes a complete point snapshot and explicit resolved CRS/zone settings. `ApplyResponse` rejects malformed/version-mismatched replies, different point counts, changed metadata/heights, invalid numbers/zones and a table that has changed since the snapshot. It validates every row before replacing all points once, so one Ctrl+Z restores the entire conversion.

The future UI consumer must also verify that its source/target CRS and zone settings still match the request before applying an asynchronous result. This transport service does not own those UI settings.

## Verification and next gate

Portable tests exercise both JavaScript hosts and the actual C# request → JS conversion → C# response application, including undo. Independent PROJ controls cover every established regional fixture. This proves the transport and calculation adapter, not an embedded runtime or Windows UI.

Next: select and verify a browser-free embedded JS host against these exact files and control set, then wire native selectors and conversion. Do not enable native conversion or remove WebView2 before that verification.
