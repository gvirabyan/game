using UnityEngine;

namespace Corehold
{
    // Look of each campaign zone: ground, road and sky colours plus the vegetation that grows there.
    // Used by both the missions (LevelView) and the campaign map (WorldMapView).
    public sealed class BiomeStyle
    {
        public Color GrassLight, GrassDark, GrassDry, Slope, HillTop;
        public Color DirtLight, DirtDark, Verge, Rut;
        public Color Sky;
        public Color Leaf, LeafLight;

        static readonly BiomeStyle[] styles =
        {
            new BiomeStyle   // grass
            {
                GrassLight = Art.Hex("74A843"), GrassDark = Art.Hex("4A8436"), GrassDry = Art.Hex("A0A852"), Slope = Art.Hex("9A8F78"), HillTop = Art.Hex("8DBA55"),
                DirtLight = Art.Hex("C7A06A"), DirtDark = Art.Hex("A07E50"), Verge = Art.Hex("7E7A44"), Rut = Art.Hex("8C6C44"),
                Sky = Art.Hex("BCD3C8"), Leaf = Art.Hex("4C8A38"), LeafLight = Art.Hex("6EA84C"),
            },
            new BiomeStyle   // desert
            {
                GrassLight = Art.Hex("E0C08A"), GrassDark = Art.Hex("C9A46A"), GrassDry = Art.Hex("D8B070"), Slope = Art.Hex("B07A50"), HillTop = Art.Hex("EBD2A0"),
                DirtLight = Art.Hex("B88A58"), DirtDark = Art.Hex("9A6E44"), Verge = Art.Hex("BD9862"), Rut = Art.Hex("8A6440"),
                Sky = Art.Hex("E6D3B0"), Leaf = Art.Hex("8C9A4A"), LeafLight = Art.Hex("A6B05C"),
            },
            new BiomeStyle   // snow
            {
                GrassLight = Art.Hex("EEF3F6"), GrassDark = Art.Hex("CDD8E0"), GrassDry = Art.Hex("DCE5EC"), Slope = Art.Hex("8A8F96"), HillTop = Art.Hex("FFFFFF"),
                DirtLight = Art.Hex("A89A88"), DirtDark = Art.Hex("8A7C6C"), Verge = Art.Hex("C4C8C8"), Rut = Art.Hex("7A6E62"),
                Sky = Art.Hex("D3DEE8"), Leaf = Art.Hex("2E5A44"), LeafLight = Art.Hex("3E6E54"),
            },
            new BiomeStyle   // autumn
            {
                GrassLight = Art.Hex("A8A048"), GrassDark = Art.Hex("7E7A34"), GrassDry = Art.Hex("C09A4A"), Slope = Art.Hex("8E7A64"), HillTop = Art.Hex("B8A858"),
                DirtLight = Art.Hex("A07850"), DirtDark = Art.Hex("80603E"), Verge = Art.Hex("8A6E3E"), Rut = Art.Hex("6E5234"),
                Sky = Art.Hex("E2CDB4"), Leaf = Art.Hex("D9822B"), LeafLight = Art.Hex("E8A83A"),
            },
        };

        public static BiomeStyle Of(Biome biome) => styles[(int)biome];

        // A big plant for this zone (trees, cacti, snowy pines, autumn trees).
        public void Tree(MeshBuilder b, Vector3 at, float size, int seed, Biome biome)
        {
            switch (biome)
            {
                case Biome.Desert: Models.Cactus(b, at, size, seed); break;
                case Biome.Snow: Models.SnowPine(b, at, size); break;
                case Biome.Autumn: Models.Tree(b, at, size, seed, Leaf, LeafLight); break;
                default: Models.Tree(b, at, size, seed); break;
            }
        }

        // A small plant for this zone.
        public void Bush(MeshBuilder b, Vector3 at, float size, int seed, Biome biome)
        {
            switch (biome)
            {
                case Biome.Desert: Models.DryBush(b, at, size, seed); break;
                case Biome.Snow: Models.Rock(b, at, size, seed, true); break;
                case Biome.Autumn: Models.Bush(b, at, size, seed, Leaf, LeafLight); break;
                default: Models.Bush(b, at, size, seed); break;
            }
        }
    }
}
