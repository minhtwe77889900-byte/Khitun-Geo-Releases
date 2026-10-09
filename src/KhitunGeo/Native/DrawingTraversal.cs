namespace KhitunGeo.Native;

internal readonly record struct DrawingTraversalStats(int Visited, int Emitted, bool Truncated);
internal static class DrawingTraversal
{
    public static DrawingTraversalStats Visit(DrawingScene scene, IReadOnlySet<string> visibleLayers, DrawingBounds? clip, Action<DrawingPrimitive> emit, Func<bool>? shouldStop = null)
    {
        var visited = 0; var emitted = 0; var truncated = false;
        string Layer(string value, string inherited) => value == "0" ? inherited : value;
        void Walk(DrawingDefinition definition, Affine2D transform, string inherited, int depth)
        {
            if (depth > 32) { truncated = true; return; }
            foreach (var node in definition.Nodes)
            {
                if (visited >= 200000 || shouldStop?.Invoke() == true) { truncated = true; return; }
                visited++;
                if (node is DrawingInsertNode insert)
                {
                    var layer = Layer(insert.Layer, inherited);
                    if (!visibleLayers.Contains(layer)) continue;
                    var next = transform.Compose(insert.Transform);
                    var child = scene.Definitions[insert.DefinitionId];
                    var bounds = child.Bounds.Transform(next);
                    if (!next.IsFinite || (!bounds.IsEmpty && !bounds.IsFinite)) { truncated = true; continue; }
                    if (bounds.IsEmpty || (clip.HasValue && !bounds.Intersects(clip.Value))) continue;
                    Walk(child, next, layer, depth + 1);
                }
                else if (node is DrawingGeometryNode geometry)
                {
                    var layer = Layer(geometry.Primitive.Layer, inherited);
                    if (!visibleLayers.Contains(layer)) continue;
                    var p = geometry.Primitive.Transform(transform) with { Layer = layer };
                    if (!p.IsValid) { truncated = true; continue; }
                    if (clip.HasValue && !p.Bounds.Intersects(clip.Value)) continue;
                    emitted++; emit(p);
                }
            }
        }
        Walk(scene.Root, Affine2D.Identity, "0", 0);
        return new(visited, emitted, truncated);
    }
}
