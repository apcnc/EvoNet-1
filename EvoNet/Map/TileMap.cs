﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace EvoNet.Map
{
    public class TileMap : UpdateModule
    {
        public const float MAXIMUMFOODPERTILE = 100;

        float[,] foodValues;
        private TileType[,] types;
        Rectangle[,] renderRectangles;
        Rectangle[,] rendersourceRectangles;

        Texture2D Water1Texture { get; set; }
        Texture2D Water2Texture { get; set; }
        Texture2D GrassTexture { get; set; }
        Texture2D SandTexture { get; set; }
        Texture2D BlendMap { get; set; }
        Effect LandShader { get; set; }
        Effect WaterShader { get; set; }

        float tileSize;
        public List<float> FoodRecord = new List<float>();


        SpriteBatch spriteBatch;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public float[,] FoodValues
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return foodValues;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                foodValues = value;
            }
        }

        private TileType[,] Types
        {
            get
            {
                return types;
            }

            set
            {
                types = value;
            }
        }

        public void SetTileType(int x, int y, TileType tt)
        {
            types[x, y] = tt;
            if(tt != TileType.Land)
            {
                foodValues[x, y] = 0;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool SetFoodValue(int x, int y, float foodValue)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {

                if (types[x, y] == TileType.Land)
                {
                    foodValues[x, y] = foodValue;
                    return true;
                }
            }
            return false;
        }

        public override bool WantsFastForward
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Creates a new tilemap
        /// </summary>
        /// <param name="width">Number of tiles in horizontal direction</param>
        /// <param name="height">Number of tiles in vertical direction</param>
        /// <param name="inTileSize">Width and Height of a Tile</param>
        /// <param name="renderTextureTileFactor">How many tiles use a single texture until it wraps?
        /// Setting this to a higher value reduces tiled look</param>
        public TileMap(int width, int height, float inTileSize, int renderTextureTileFactor = 5)
        {
            Width = width;
            Height = height;
            foodValues = new float[width, height];
            types = new TileType[width, height];
            renderRectangles = new Rectangle[width, height];
            rendersourceRectangles = new Rectangle[width, height];

            int textureSize = 512; // Assume we have a 512x512 texture
            int sourceSize = textureSize / renderTextureTileFactor;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    foodValues[x, y] = MAXIMUMFOODPERTILE;
                    renderRectangles[x, y] = new Rectangle(
                        (int)(x * inTileSize),
                        (int)(y * inTileSize),
                        (int)inTileSize,
                        (int)inTileSize);
                    rendersourceRectangles[x, y] = new Rectangle(
                        x * textureSize / renderTextureTileFactor,
                        y * textureSize / renderTextureTileFactor,
                        sourceSize,
                        sourceSize);
                }
            }
            tileSize = inTileSize;
        }

        public Tile GetTileInfo(int x, int y)
        {
            return new Tile(new Point(x, y), types[x, y], foodValues[x, y]);
        }

        public Tile GetTileInfo(Point position)
        {
            if(position.X < 0 || position.X > Width - 1 || position.Y < 0 || position.Y > Height - 1)
            {
                return new Tile(position, TileType.None, 0);
            }
            return new Tile(position, types[position.X, position.Y], foodValues[position.X, position.Y]);
        }

        public Tile GetTileAtWorldPosition(Vector2 position)
        {
            position /= tileSize;
            return GetTileInfo(position.ToPoint());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public float EatOfTile(int x, int y, float eatAmount)
        {
            float foodVal = FoodValues[x, y];
            if (foodVal > eatAmount)
            {
                FoodValues[x, y] -= eatAmount;
                return eatAmount;
            }
            else
            {
                FoodValues[x, y] = 0;
                return foodVal;
            }
        }

        public float GetWorldWidth()
        {
            return Width * tileSize;
        }

        public float GetWorldHeight()
        {
            return Height * tileSize;
        }

        public float CalculateFoodAvailable()
        {
            float food = 0;
            for(int i = 0; i<Width; i++)
            {
                for(int k = 0; k<Height; k++)
                {
                    food += FoodValues[i, k];
                }
            }
            return food;
        }

        public override void Initialize(EvoGame game)
        {
            base.Initialize(game);
            spriteBatch = new SpriteBatch(game.GraphicsDevice);

            SandTexture = game.Content.Load<Texture2D>("Map/SandTexture");
            GrassTexture = game.Content.Load<Texture2D>("Map/GrassTexture");
            BlendMap = game.Content.Load<Texture2D>("Map/BlendMap");
            Water1Texture = game.Content.Load<Texture2D>("Map/Water1");
            Water2Texture = game.Content.Load<Texture2D>("Map/Water2");
            BlendMap = game.Content.Load<Texture2D>("Map/BlendMap");
            LandShader = game.Content.Load<Effect>("Map/GrassDisplay");
            WaterShader = game.Content.Load<Effect>("Map/WaterEffect");

            LandShader.Parameters["GrassTexture"].SetValue(GrassTexture);
            LandShader.Parameters["SandTexture"].SetValue(SandTexture);
            LandShader.Parameters["BlendMap"].SetValue(BlendMap);
            WaterShader.Parameters["Water2"].SetValue(Water2Texture);
        }

        protected override void Update(GameTime deltaTime)
        {
            float fixedDeltaTime = (float)deltaTime.ElapsedGameTime.TotalSeconds;
            for (int i = 0; i < Width; i++)
            {
                for (int k = 0; k < Height; k++)
                {
                    if (IsFertile(i, k))
                    {
                        Grow(i, k, fixedDeltaTime);
                    }
                }
            }

            FoodRecord.Add(CalculateFoodAvailable());
        }

        public void Grow(int x, int y, float fixedDeltaTime)
        {
            foodValues[x, y] += 20f * fixedDeltaTime;
            if (foodValues[x, y] > MAXIMUMFOODPERTILE) foodValues[x, y] = MAXIMUMFOODPERTILE;
        }

        public bool IsFertileToNeighbors(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return false; //If out of bounds
            }
            if (types[x, y] == TileType.Water)
            {
                return true;
            }
            if (types[x, y] == TileType.Land && foodValues[x, y] > 50)
            {
                return true;
            }
            return false;
        }

        public bool IsFertile(int x, int y)
        {
            if (types[x, y] == TileType.Land)
            {
                if (foodValues[x, y] > 50)
                {
                    return true;
                }
                if (IsFertileToNeighbors(x - 1, y))
                {
                    return true;
                }
                if (IsFertileToNeighbors(x + 1, y))
                {
                    return true;
                }
                if (IsFertileToNeighbors(x, y - 1))
                {
                    return true;
                }
                if (IsFertileToNeighbors(x, y + 1))
                {
                    return true;
                }
            }

            return false;
        }

        public void Draw(GameTime deltaTime)
        {
            Matrix? UsedMatrix = null;
            UsedMatrix = Camera.instanceGameWorld.Matrix;

            // Render land tiles with shader effect to blend between sand and grass
            spriteBatch.Begin(transformMatrix: UsedMatrix, effect: LandShader);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (types[x, y] == TileType.Land)
                    {
                        Color color = new Color(0.0f, 0.0f, 1.0f, 1 - foodValues[x, y] / 100.0f);
                        spriteBatch.Draw(SandTexture, renderRectangles[x, y], rendersourceRectangles[x, y], color);
                    }
                }
            }
            spriteBatch.End();

            // Render water tiles with animated "water" shader
            spriteBatch.Begin(transformMatrix: UsedMatrix, effect: WaterShader);
            WaterShader.Parameters["Time"].SetValue((float)deltaTime.TotalGameTime.TotalSeconds / 3);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (types[x, y] == TileType.Water)
                    {
                        spriteBatch.Draw(Water1Texture, renderRectangles[x, y], rendersourceRectangles[x, y], Color.White);
                    }
                }
            }
            spriteBatch.End();

        }

        public void SerializeToFile(string fileName)
        {
            FileStream file = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write);
            BinaryWriter writer = new BinaryWriter(file);
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(tileSize);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    writer.Write((int)types[x, y]);
                    if (types[x,y] == TileType.Land)
                    {
                        writer.Write(foodValues[x, y]);
                    }
                }
            }
            file.Close();
        }

        public static TileMap DeserializeFromFile(string fileName, EvoGame game)
        {
            try
            {
                FileStream file = File.Open(fileName, FileMode.Open);
                BinaryReader reader = new BinaryReader(file);
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();
                float tileSize = reader.ReadSingle();
                TileMap result = new TileMap(width, height, tileSize);
                result.Initialize(game);
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        TileType type = (TileType)reader.ReadInt32();
                        result.SetTileType(x, y, type);
                        if (type == TileType.Land)
                        {
                            float foodValue = reader.ReadSingle();
                            result.SetFoodValue(x, y, foodValue);
                        }
                    }
                }
                file.Close();
                return result;
            }
            catch (System.IO.FileNotFoundException)
            {
                return null;
            }

        }


    }
}
