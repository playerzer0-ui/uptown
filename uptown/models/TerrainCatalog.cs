namespace NodeTesting.models;

public static class TerrainCatalog
{
    public static readonly string[] Names = { "basic", "wall", "glass", "grass", "ground", "floor" };
    public static string[] Paths()
    {
        var paths = new string[Names.Length];
        for (int i = 0; i < paths.Length; i++) paths[i] = "graphics/tileset/" + Names[i];
        return paths;
    }
}
