namespace NodeTesting.models
{
    public class TileMap : Map
    {
        public TileMap(string texturePath, int tileWidth, int tileHeight, string csvPath)
            : base(texturePath, tileWidth, tileHeight)
        {
            LoadCSV(csvPath);
        }
    }
}