using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley.Enchantments;
using StardewValley.Extensions;
using StardewValley.GameData.FruitTrees;
using StardewValley.Internal;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TokenizableStrings;
using StardewValley.Tools;

namespace StardewValley.TerrainFeatures;

public class FruitTree : TerrainFeature
{
	/// <summary>The asset name for the default fruit tree tilesheet.</summary>
	public const string DefaultTextureName = "TileSheets\\fruitTrees";

	public const float shakeRate = (float)Math.PI / 200f;

	public const float shakeDecayRate = 0.0030679617f;

	public const int minWoodDebrisForFallenTree = 12;

	public const int minWoodDebrisForStump = 5;

	public const int startingHealth = 10;

	public const int leafFallRate = 3;

	public const int DaysUntilMaturity = 28;

	public const int maxFruitsOnTrees = 3;

	public const int seedStage = 0;

	public const int sproutStage = 1;

	public const int saplingStage = 2;

	public const int bushStage = 3;

	public const int treeStage = 4;

	/// <summary>The texture from which to draw the tree sprites.</summary>
	[XmlIgnore]
	public Texture2D texture;

	[XmlElement("growthStage")]
	public readonly NetInt growthStage = new NetInt();

	/// <summary>Obsolete. This is only kept to preserve data from old save files. Use <see cref="F:StardewValley.TerrainFeatures.FruitTree.treeId" /> instead.</summary>
	[XmlElement("treeType")]
	public string obsolete_treeType;

	/// <summary>The unique identifier for the underlying fruit tree data.</summary>
	[XmlElement("treeId")]
	public readonly NetString treeId = new NetString();

	/// <summary>The number of days until the fruit tree becomes full-grown.</summary>
	/// <remarks>The fruit tree is a seed at <see cref="F:StardewValley.TerrainFeatures.FruitTree.DaysUntilMaturity" /> and becomes full-grown at 0 or below.</remarks>
	[XmlElement("daysUntilMature")]
	public readonly NetInt daysUntilMature = new NetInt(28);

	/// <summary>Obsolete. This is only kept to preserve data from old save files. Use <see cref="F:StardewValley.TerrainFeatures.FruitTree.fruit" /> instead.</summary>
	[XmlElement("fruitsOnTree")]
	public int? obsolete_fruitsOnTree;

	[XmlElement("fruit")]
	public readonly NetList<Item, NetRef<Item>> fruit = new NetList<Item, NetRef<Item>>();

	[XmlElement("struckByLightningCountdown")]
	public readonly NetInt struckByLightningCountdown = new NetInt();

	[XmlElement("health")]
	public readonly NetFloat health = new NetFloat(10f);

	[XmlElement("flipped")]
	public readonly NetBool flipped = new NetBool();

	[XmlElement("stump")]
	public readonly NetBool stump = new NetBool();

	/// <summary>Whether the tree is planted on a stone tile in the greenhouse.</summary>
	[XmlElement("greenHouseTileTree")]
	public readonly NetBool greenHouseTileTree = new NetBool();

	[XmlIgnore]
	public readonly NetBool shakeLeft = new NetBool();

	[XmlIgnore]
	public readonly NetBool falling = new NetBool();

	[XmlIgnore]
	public bool destroy;

	[XmlIgnore]
	public float shakeRotation;

	[XmlIgnore]
	public float maxShake;

	[XmlIgnore]
	public float alpha = 1f;

	private List<Leaf> leaves = new List<Leaf>();

	[XmlIgnore]
	public readonly NetLong lastPlayerToHit = new NetLong();

	[XmlIgnore]
	public float shakeTimer;

	[XmlElement("growthRate")]
	public readonly NetInt growthRate = new NetInt(1);

	/// <summary>The asset name loaded for <see cref="F:StardewValley.TerrainFeatures.FruitTree.texture" />.</summary>
	[XmlIgnore]
	public string textureName { get; private set; }

	/// <inheritdoc cref="F:StardewValley.TerrainFeatures.FruitTree.greenHouseTileTree" />
	[XmlIgnore]
	public bool GreenHouseTileTree
	{
		get
		{
			return this.greenHouseTileTree;
		}
		set
		{
			this.greenHouseTileTree.Value = value;
		}
	}

	public FruitTree()
		: this(null)
	{
	}

	public FruitTree(string id, int growthStage = 0)
		: base(needsTick: true)
	{
		this.treeId.Value = id;
		this.growthStage.Value = growthStage;
		this.daysUntilMature.Value = FruitTree.GrowthStageToDaysUntilMature(growthStage);
		this.flipped.Value = Game1.random.NextBool();
		this.loadSprite();
	}

	public override void initNetFields()
	{
		base.initNetFields();
		base.NetFields.AddField(this.growthStage, "this.growthStage").AddField(this.treeId, "treeId").AddField(this.daysUntilMature, "daysUntilMature")
			.AddField(this.fruit, "fruit")
			.AddField(this.struckByLightningCountdown, "struckByLightningCountdown")
			.AddField(this.health, "health")
			.AddField(this.flipped, "flipped")
			.AddField(this.stump, "stump")
			.AddField(this.greenHouseTileTree, "greenHouseTileTree")
			.AddField(this.shakeLeft, "shakeLeft")
			.AddField(this.falling, "falling")
			.AddField(this.lastPlayerToHit, "lastPlayerToHit")
			.AddField(this.growthRate, "growthRate");
		this.treeId.fieldChangeVisibleEvent += delegate
		{
			this.loadSprite();
		};
	}

	public int GetSpriteRowNumber()
	{
		return this.GetData()?.TextureSpriteRow ?? 0;
	}

	public override void loadSprite()
	{
		string assetName = this.GetData()?.Texture ?? "TileSheets\\fruitTrees";
		if (this.texture == null || this.textureName != assetName)
		{
			try
			{
				this.texture = Game1.content.Load<Texture2D>(assetName);
				this.textureName = assetName;
			}
			catch (Exception ex)
			{
				Game1.log.Error($"Fruit tree '{this.treeId.Value}' failed to load spritesheet '{assetName}'.", ex);
			}
		}
	}

	public override bool isActionable()
	{
		return true;
	}

	/// <summary>Get whether the tree is in a location which ignores seasons (like the greenhouse or Ginger Island).</summary>
	public bool IgnoresSeasonsHere()
	{
		return this.Location?.SeedsIgnoreSeasonsHere() ?? false;
	}

	public override Rectangle getBoundingBox()
	{
		Vector2 tileLocation = this.Tile;
		return new Rectangle((int)tileLocation.X * 64, (int)tileLocation.Y * 64, 64, 64);
	}

	public override Rectangle getRenderBounds()
	{
		Vector2 tileLocation = this.Tile;
		if ((bool)this.stump || (int)this.growthStage < 4)
		{
			return new Rectangle((int)(tileLocation.X - 0f) * 64, (int)(tileLocation.Y - 1f) * 64, 64, 128);
		}
		return new Rectangle((int)(tileLocation.X - 1f) * 64, (int)(tileLocation.Y - 5f) * 64, 192, 448);
	}

	public override bool performUseAction(Vector2 tileLocation)
	{
		GameLocation location = this.Location;
		if (this.maxShake == 0f && !this.stump && (int)this.growthStage >= 3 && !this.IsWinterTreeHere())
		{
			location.playSound("leafrustle");
		}
		this.shake(tileLocation, doEvenIfStillShaking: false);
		return true;
	}

	public override bool tickUpdate(GameTime time)
	{
		if (this.destroy)
		{
			return true;
		}
		GameLocation location = this.Location;
		Vector2 tileLocation = this.Tile;
		this.alpha = Math.Min(1f, this.alpha + 0.05f);
		if (this.shakeTimer > 0f)
		{
			this.shakeTimer -= time.ElapsedGameTime.Milliseconds;
		}
		if ((int)this.growthStage >= 4 && !this.falling && !this.stump && Game1.player.GetBoundingBox().Intersects(new Rectangle(64 * ((int)tileLocation.X - 1), 64 * ((int)tileLocation.Y - 4), 192, 224)))
		{
			this.alpha = Math.Max(0.4f, this.alpha - 0.09f);
		}
		if (!this.falling)
		{
			if ((double)Math.Abs(this.shakeRotation) > Math.PI / 2.0 && this.leaves.Count <= 0 && this.health.Value <= 0f)
			{
				return true;
			}
			if (this.maxShake > 0f)
			{
				if ((bool)this.shakeLeft)
				{
					this.shakeRotation -= (((int)this.growthStage >= 4) ? 0.005235988f : ((float)Math.PI / 200f));
					if (this.shakeRotation <= 0f - this.maxShake)
					{
						this.shakeLeft.Value = false;
					}
				}
				else
				{
					this.shakeRotation += (((int)this.growthStage >= 4) ? 0.005235988f : ((float)Math.PI / 200f));
					if (this.shakeRotation >= this.maxShake)
					{
						this.shakeLeft.Value = true;
					}
				}
			}
			if (this.maxShake > 0f)
			{
				this.maxShake = Math.Max(0f, this.maxShake - (((int)this.growthStage >= 4) ? 0.0010226539f : 0.0030679617f));
			}
			if ((int)this.struckByLightningCountdown > 0 && Game1.random.NextDouble() < 0.01)
			{
				Game1.multiplayer.broadcastSprites(location, new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(372, 1956, 10, 10), new Vector2(tileLocation.X * 64f + (float)Game1.random.Next(-64, 96), tileLocation.Y * 64f - 192f + (float)Game1.random.Next(-64, 128)), flipped: false, 0.002f, Color.Gray)
				{
					alpha = 0.75f,
					motion = new Vector2(0f, -0.5f),
					interval = 99999f,
					layerDepth = 1f,
					scale = 2f,
					scaleChange = 0.01f
				});
			}
		}
		else
		{
			this.shakeRotation += (this.shakeLeft ? (0f - this.maxShake * this.maxShake) : (this.maxShake * this.maxShake));
			this.maxShake += 0.0015339808f;
			if (Game1.random.NextDouble() < 0.01 && !this.IsWinterTreeHere())
			{
				location.localSound("leafrustle");
			}
			if ((double)Math.Abs(this.shakeRotation) > Math.PI / 2.0)
			{
				this.falling.Value = false;
				this.maxShake = 0f;
				location.localSound("treethud");
				int leavesToAdd = Game1.random.Next(90, 120);
				for (int j = 0; j < leavesToAdd; j++)
				{
					this.leaves.Add(new Leaf(new Vector2(Game1.random.Next((int)(tileLocation.X * 64f), (int)(tileLocation.X * 64f + 192f)) + (this.shakeLeft ? (-320) : 256), tileLocation.Y * 64f - 64f), (float)Game1.random.Next(-10, 10) / 100f, Game1.random.Next(4), (float)Game1.random.Next(10, 40) / 10f));
				}
				Game1.createRadialDebris(location, 12, (int)tileLocation.X + (this.shakeLeft ? (-4) : 4), (int)tileLocation.Y, (int)((Game1.getFarmer(this.lastPlayerToHit.Value).professions.Contains(12) ? 1.25 : 1.0) * 12.0), resource: true);
				Game1.createRadialDebris(location, 12, (int)tileLocation.X + (this.shakeLeft ? (-4) : 4), (int)tileLocation.Y, (int)((Game1.getFarmer(this.lastPlayerToHit.Value).professions.Contains(12) ? 1.25 : 1.0) * 12.0), resource: false);
				if (Game1.IsMultiplayer)
				{
					Game1.recentMultiplayerRandom = Utility.CreateRandom((double)tileLocation.X * 1000.0, tileLocation.Y);
				}
				if (Game1.IsMultiplayer)
				{
					Game1.createMultipleObjectDebris("(O)92", (int)tileLocation.X + (this.shakeLeft ? (-4) : 4), (int)tileLocation.Y, 10, this.lastPlayerToHit.Value, location);
				}
				else
				{
					Game1.createMultipleObjectDebris("(O)92", (int)tileLocation.X + (this.shakeLeft ? (-4) : 4), (int)tileLocation.Y, 10, location);
				}
				if (this.health.Value <= 0f)
				{
					this.health.Value = -100f;
				}
			}
		}
		for (int i = this.leaves.Count - 1; i >= 0; i--)
		{
			Leaf leaf = this.leaves[i];
			leaf.position.Y -= leaf.yVelocity - 3f;
			leaf.yVelocity = Math.Max(0f, leaf.yVelocity - 0.01f);
			leaf.rotation += leaf.rotationRate;
			if (leaf.position.Y >= tileLocation.Y * 64f + 64f)
			{
				this.leaves.RemoveAt(i);
			}
		}
		return false;
	}

	/// <summary>Get the quality of fruit currently produced by the tree, matching one of the constants like <see cref="F:StardewValley.Object.highQuality" />.</summary>
	public int GetQuality()
	{
		if ((int)this.struckByLightningCountdown > 0 || (int)this.daysUntilMature >= 0)
		{
			return 0;
		}
		return ((int)this.daysUntilMature / -112) switch
		{
			0 => 0, 
			1 => 1, 
			2 => 2, 
			_ => 4, 
		};
	}

	public virtual void shake(Vector2 tileLocation, bool doEvenIfStillShaking)
	{
		if ((this.maxShake == 0f || doEvenIfStillShaking) && (int)this.growthStage >= 3 && !this.stump)
		{
			Vector2 playerPixel = Game1.player.getStandingPosition();
			this.shakeLeft.Value = playerPixel.X > (tileLocation.X + 0.5f) * 64f || (Game1.player.Tile.X == tileLocation.X && Game1.random.NextBool());
			this.maxShake = (float)(((int)this.growthStage >= 4) ? (Math.PI / 128.0) : (Math.PI / 64.0));
			if ((int)this.growthStage >= 4)
			{
				if (Game1.random.NextDouble() < 0.66 && !this.IsWinterTreeHere())
				{
					int numberOfLeaves = Game1.random.Next(1, 6);
					for (int j = 0; j < numberOfLeaves; j++)
					{
						this.leaves.Add(new Leaf(new Vector2(Game1.random.Next((int)(tileLocation.X * 64f - 64f), (int)(tileLocation.X * 64f + 128f)), Game1.random.Next((int)(tileLocation.Y * 64f - 256f), (int)(tileLocation.Y * 64f - 192f))), (float)Game1.random.Next(-10, 10) / 100f, Game1.random.Next(4), (float)Game1.random.Next(5) / 10f));
					}
				}
				int fruitQuality = this.GetQuality();
				if (!this.Location.terrainFeatures.TryGetValue(tileLocation, out var terrainFeature) || !terrainFeature.Equals(this))
				{
					return;
				}
				for (int i = 0; i < this.fruit.Count; i++)
				{
					Vector2 offset = new Vector2(0f, 0f);
					switch (i)
					{
					case 0:
						offset.X = -64f;
						break;
					case 1:
						offset.X = 64f;
						offset.Y = -32f;
						break;
					case 2:
						offset.Y = 32f;
						break;
					}
					Debris d;
					if ((int)this.struckByLightningCountdown <= 0)
					{
						Item item = this.fruit[i];
						this.fruit[i] = null;
						d = new Debris(item, new Vector2(tileLocation.X * 64f + 32f, (tileLocation.Y - 3f) * 64f + 32f) + offset, playerPixel)
						{
							itemQuality = fruitQuality
						};
					}
					else
					{
						d = new Debris(382.ToString(), new Vector2(tileLocation.X * 64f + 32f, (tileLocation.Y - 3f) * 64f + 32f) + offset, playerPixel)
						{
							itemQuality = fruitQuality
						};
					}
					d.Chunks[0].xVelocity.Value += (float)Game1.random.Next(-10, 11) / 10f;
					d.chunkFinalYLevel = (int)(tileLocation.Y * 64f + 64f);
					this.Location.debris.Add(d);
				}
				this.fruit.Clear();
			}
			else if (Game1.random.NextDouble() < 0.66 && !this.IsWinterTreeHere())
			{
				int numberOfLeaves2 = Game1.random.Next(1, 3);
				for (int k = 0; k < numberOfLeaves2; k++)
				{
					this.leaves.Add(new Leaf(new Vector2(Game1.random.Next((int)(tileLocation.X * 64f), (int)(tileLocation.X * 64f + 48f)), tileLocation.Y * 64f - 96f), (float)Game1.random.Next(-10, 10) / 100f, Game1.random.Next(4), (float)Game1.random.Next(30) / 10f));
				}
			}
		}
		else if ((bool)this.stump)
		{
			this.shakeTimer = 100f;
		}
	}

	public override bool isPassable(Character c = null)
	{
		if (this.health.Value <= -99f)
		{
			return true;
		}
		return false;
	}

	public static bool IsTooCloseToAnotherTree(Vector2 tileLocation, GameLocation environment, bool fruitTreesOnly = false)
	{
		Vector2 v = default(Vector2);
		for (int i = (int)tileLocation.X - 2; i <= (int)tileLocation.X + 2; i++)
		{
			for (int j = (int)tileLocation.Y - 2; j <= (int)tileLocation.Y + 2; j++)
			{
				v.X = i;
				v.Y = j;
				if (environment.terrainFeatures.TryGetValue(v, out var feature) && (feature is FruitTree || (!fruitTreesOnly && feature is Tree)))
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool IsGrowthBlocked(Vector2 tileLocation, GameLocation environment)
	{
		Vector2[] surroundingTileLocationsArray = Utility.getSurroundingTileLocationsArray(tileLocation);
		for (int i = 0; i < surroundingTileLocationsArray.Length; i++)
		{
			Vector2 v = surroundingTileLocationsArray[i];
			TerrainFeature terrainFeature;
			bool isClearHoeDirt = environment.terrainFeatures.TryGetValue(v, out terrainFeature) && terrainFeature is HoeDirt dirt && dirt.crop == null;
			if (environment.IsTileOccupiedBy(v, ~(CollisionMask.Characters | CollisionMask.Farmers)) && !isClearHoeDirt)
			{
				Object o = environment.getObjectAtTile((int)v.X, (int)v.Y);
				if (o == null)
				{
					return true;
				}
				if (!(o.QualifiedItemId == "(O)590"))
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>Get the fruit tree's data from <see cref="F:StardewValley.Game1.fruitTreeData" />, if found.</summary>
	public FruitTreeData GetData()
	{
		if (!FruitTree.TryGetData(this.treeId.Value, out var data))
		{
			return null;
		}
		return data;
	}

	/// <summary>Try to get a fruit tree's data from <see cref="F:StardewValley.Game1.fruitTreeData" />.</summary>
	/// <param name="id">The tree type ID (i.e. the key in <see cref="F:StardewValley.Game1.fruitTreeData" />).</param>
	/// <param name="data">The fruit tree data, if found.</param>
	/// <returns>Returns whether the fruit tree data was found.</returns>
	public static bool TryGetData(string id, out FruitTreeData data)
	{
		if (id == null)
		{
			data = null;
			return false;
		}
		return Game1.fruitTreeData.TryGetValue(id, out data);
	}

	/// <summary>Get the translated display name for this tree, like 'Cherry' or 'Mango'.</summary>
	public string GetDisplayName()
	{
		return TokenParser.ParseText(this.GetData()?.DisplayName) ?? ItemRegistry.GetErrorItemName();
	}

	public override void dayUpdate()
	{
		GameLocation environment = this.Location;
		if (this.health.Value <= -99f)
		{
			this.destroy = true;
		}
		if ((int)this.struckByLightningCountdown > 0)
		{
			this.struckByLightningCountdown.Value--;
			if ((int)this.struckByLightningCountdown <= 0)
			{
				this.fruit.Clear();
			}
		}
		bool foundSomething = FruitTree.IsGrowthBlocked(this.Tile, environment);
		if (!foundSomething || (int)this.daysUntilMature <= 0)
		{
			if ((int)this.daysUntilMature > 28)
			{
				this.daysUntilMature.Value = 28;
			}
			if (this.growthRate.Value > 1)
			{
				_ = this.growthRate.Value;
			}
			this.daysUntilMature.Value -= this.growthRate;
			this.growthStage.Value = FruitTree.DaysUntilMatureToGrowthStage(this.daysUntilMature);
		}
		else if (foundSomething && this.growthStage.Value != 4)
		{
			Game1.multiplayer.broadcastGlobalMessage("Strings\\UI:FruitTree_Warning", true, null, this.GetDisplayName());
		}
		if ((bool)this.stump)
		{
			this.fruit.Clear();
		}
		else
		{
			this.TryAddFruit();
		}
	}

	/// <summary>Get the maximum <see cref="F:StardewValley.TerrainFeatures.FruitTree.daysUntilMature" /> value which would match a given growth stage.</summary>
	/// <param name="growthStage">The growth stage (matching a constant like <see cref="F:StardewValley.TerrainFeatures.FruitTree.treeStage" />).</param>
	public static int GrowthStageToDaysUntilMature(int growthStage)
	{
		if (growthStage > 4)
		{
			growthStage = 4;
		}
		return growthStage switch
		{
			4 => 0, 
			3 => 7, 
			2 => 14, 
			1 => 21, 
			_ => 28, 
		};
	}

	/// <summary>Get the growth stage (matching a constant like <see cref="F:StardewValley.TerrainFeatures.FruitTree.treeStage" />) for a given <see cref="F:StardewValley.TerrainFeatures.FruitTree.daysUntilMature" /> value.</summary>
	/// <param name="daysUntilMature">The <see cref="F:StardewValley.TerrainFeatures.FruitTree.daysUntilMature" /> value.</param>
	public static int DaysUntilMatureToGrowthStage(int daysUntilMature)
	{
		for (int stage = 4; stage >= 0; stage--)
		{
			if (daysUntilMature <= FruitTree.GrowthStageToDaysUntilMature(stage))
			{
				return stage;
			}
		}
		return 0;
	}

	/// <summary>Try to add a fruit to the tree.</summary>
	public bool TryAddFruit()
	{
		if (!this.stump.Value && this.growthStage.Value >= 4 && (this.IsInSeasonHere() || (this.struckByLightningCountdown.Value > 0 && !this.IsWinterTreeHere())) && this.fruit.Count < 3)
		{
			FruitTreeData data = this.GetData();
			if (data?.Fruit != null)
			{
				foreach (FruitTreeFruitData entry in data.Fruit)
				{
					Item item = this.TryCreateFruit(entry);
					if (item != null)
					{
						this.fruit.Add(item);
						return true;
					}
				}
			}
		}
		return false;
	}

	/// <summary>Create a fruit item if its fields match.</summary>
	/// <param name="drop">The fruit data.</param>
	/// <returns>Returns the produced item (if any), else <c>null</c>.</returns>
	private Item TryCreateFruit(FruitTreeFruitData drop)
	{
		if (!Game1.random.NextBool(drop.Chance))
		{
			return null;
		}
		if (drop.Condition != null && !GameStateQuery.CheckConditions(drop.Condition, this.Location, null, null, null, null, this.IgnoresSeasonsHere() ? GameStateQuery.SeasonQueryKeys : null))
		{
			return null;
		}
		if (drop.Season.HasValue && !this.IgnoresSeasonsHere() && drop.Season != Game1.GetSeasonForLocation(this.Location))
		{
			return null;
		}
		Item item = ItemQueryResolver.TryResolveRandomItem(drop, new ItemQueryContext(this.Location, null, null), avoidRepeat: false, null, null, null, delegate(string query, string error)
		{
			Game1.log.Error($"Fruit tree '{this.treeId.Value}' failed parsing item query '{query}' for fruit '{drop.Id}': {error}");
		});
		if (item != null)
		{
			item.Quality = this.GetQuality();
		}
		return item;
	}

	/// <summary>Get whether the fruit tree is in winter mode now (e.g. with no leaves).</summary>
	public virtual bool IsWinterTreeHere()
	{
		if (!this.IgnoresSeasonsHere())
		{
			return Game1.GetSeasonForLocation(this.Location) == Season.Winter;
		}
		return false;
	}

	/// <summary>Get whether the fruit tree can produce fruit now.</summary>
	public virtual bool IsInSeasonHere()
	{
		if (this.IgnoresSeasonsHere())
		{
			return true;
		}
		List<Season> growSeasons = this.GetData()?.Seasons;
		if (growSeasons != null && growSeasons.Count > 0)
		{
			Season curSeason = Game1.GetSeasonForLocation(this.Location);
			foreach (Season growSeason in growSeasons)
			{
				if (curSeason == growSeason)
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <inheritdoc />
	public override bool seasonUpdate(bool onLoad)
	{
		if (!this.IsInSeasonHere() && !onLoad)
		{
			this.fruit.Clear();
		}
		return false;
	}

	public override bool performToolAction(Tool t, int explosion, Vector2 tileLocation)
	{
		if (this.health.Value <= -99f)
		{
			return false;
		}
		if (t is MeleeWeapon)
		{
			return false;
		}
		GameLocation location = this.Location;
		if ((int)this.growthStage >= 4)
		{
			if (t is Axe)
			{
				location.playSound("axchop", tileLocation);
				location.debris.Add(new Debris(12, Game1.random.Next((int)t.upgradeLevel * 2, (int)t.upgradeLevel * 4), t.getLastFarmerToUse().GetToolLocation() + new Vector2(16f, 0f), t.getLastFarmerToUse().Position, 0));
				this.lastPlayerToHit.Value = t.getLastFarmerToUse().UniqueMultiplayerID;
				int fruitQuality = this.GetQuality();
				if (location.terrainFeatures.TryGetValue(tileLocation, out var terrainFeature) && terrainFeature.Equals(this))
				{
					for (int i = 0; i < this.fruit.Count; i++)
					{
						Vector2 offset = new Vector2(0f, 0f);
						switch (i)
						{
						case 0:
							offset.X = -64f;
							break;
						case 1:
							offset.X = 64f;
							offset.Y = -32f;
							break;
						case 2:
							offset.Y = 32f;
							break;
						}
						Debris d2;
						if ((int)this.struckByLightningCountdown <= 0)
						{
							Item item = this.fruit[i];
							this.fruit[i] = null;
							d2 = new Debris(item, new Vector2(tileLocation.X * 64f + 32f, (tileLocation.Y - 3f) * 64f + 32f) + offset, Game1.player.getStandingPosition())
							{
								itemQuality = fruitQuality
							};
						}
						else
						{
							d2 = new Debris(382.ToString(), new Vector2(tileLocation.X * 64f + 32f, (tileLocation.Y - 3f) * 64f + 32f) + offset, Game1.player.getStandingPosition())
							{
								itemQuality = fruitQuality
							};
						}
						d2.Chunks[0].xVelocity.Value += (float)Game1.random.Next(-10, 11) / 10f;
						d2.chunkFinalYLevel = (int)(tileLocation.Y * 64f + 64f);
						location.debris.Add(d2);
					}
					this.fruit.Clear();
				}
			}
			else if (explosion <= 0)
			{
				return false;
			}
			this.shake(tileLocation, doEvenIfStillShaking: true);
			float damage;
			if (explosion > 0)
			{
				damage = explosion;
			}
			else
			{
				if (t == null)
				{
					return false;
				}
				damage = t.upgradeLevel switch
				{
					0L => 1f, 
					1L => 1.25f, 
					2L => 1.67f, 
					3L => 2.5f, 
					4L => 5f, 
					_ => (int)t.upgradeLevel + 1, 
				};
			}
			this.health.Value -= damage;
			if (t is Axe && t.hasEnchantmentOfType<ShavingEnchantment>() && Game1.random.NextDouble() <= (double)(damage / 5f))
			{
				Debris d = new Debris("388", new Vector2(tileLocation.X * 64f + 32f, (tileLocation.Y - 0.5f) * 64f + 32f), Game1.player.getStandingPosition());
				d.Chunks[0].xVelocity.Value += (float)Game1.random.Next(-10, 11) / 10f;
				d.chunkFinalYLevel = (int)(tileLocation.Y * 64f + 64f);
				location.debris.Add(d);
			}
			if (this.health.Value <= 0f)
			{
				if (!this.stump)
				{
					location.playSound("treecrack", tileLocation);
					this.stump.Value = true;
					this.health.Value = 5f;
					this.falling.Value = true;
					if (t?.getLastFarmerToUse() == null)
					{
						this.shakeLeft.Value = true;
					}
					else
					{
						this.shakeLeft.Value = (float)t.getLastFarmerToUse().StandingPixel.X > (tileLocation.X + 0.5f) * 64f;
					}
				}
				else
				{
					this.health.Value = -100f;
					Game1.createRadialDebris(location, 12, (int)tileLocation.X, (int)tileLocation.Y, Game1.random.Next(30, 40), resource: false);
					if (Game1.IsMultiplayer)
					{
						Game1.recentMultiplayerRandom = Utility.CreateRandom((double)tileLocation.X * 2000.0, tileLocation.Y);
					}
					if (t?.getLastFarmerToUse() == null)
					{
						Game1.createMultipleObjectDebris("(O)92", (int)tileLocation.X, (int)tileLocation.Y, 2, location);
					}
					else if (Game1.IsMultiplayer)
					{
						Game1.createMultipleObjectDebris("(O)92", (int)tileLocation.X, (int)tileLocation.Y, 1, this.lastPlayerToHit.Value, location);
						Game1.createRadialDebris(location, 12, (int)tileLocation.X, (int)tileLocation.Y, Game1.getFarmer(this.lastPlayerToHit.Value).professions.Contains(12) ? 5 : 4, resource: true);
					}
					else
					{
						Game1.createRadialDebris(location, 12, (int)tileLocation.X, (int)tileLocation.Y, (int)((Game1.getFarmer(this.lastPlayerToHit.Value).professions.Contains(12) ? 1.25 : 1.0) * 5.0), resource: true);
						Game1.createMultipleObjectDebris("(O)92", (int)tileLocation.X, (int)tileLocation.Y, 1, location);
					}
					if (this.treeId.Value != null)
					{
						Game1.createItemDebris(ItemRegistry.Create("(O)" + this.treeId.Value, 1, this.GetQuality()), tileLocation * 64f, 2, location);
					}
				}
			}
		}
		else if ((int)this.growthStage >= 3)
		{
			if (t != null && t.BaseName.Contains("Ax"))
			{
				location.playSound("axchop", tileLocation);
				location.playSound("leafrustle", tileLocation);
				location.debris.Add(new Debris(12, Game1.random.Next((int)t.upgradeLevel * 2, (int)t.upgradeLevel * 4), t.getLastFarmerToUse().GetToolLocation() + new Vector2(16f, 0f), t.getLastFarmerToUse().getStandingPosition(), 0));
			}
			else if (explosion <= 0)
			{
				return false;
			}
			this.shake(tileLocation, doEvenIfStillShaking: true);
			float damage2 = 1f;
			Random debrisRandom = ((!Game1.IsMultiplayer) ? Utility.CreateRandom(Game1.uniqueIDForThisGame, (double)tileLocation.X * 7.0, (double)tileLocation.Y * 11.0, Game1.stats.DaysPlayed, this.health.Value) : Game1.recentMultiplayerRandom);
			if (explosion > 0)
			{
				damage2 = explosion;
			}
			else
			{
				switch (t.upgradeLevel)
				{
				case 0L:
					damage2 = 2f;
					break;
				case 1L:
					damage2 = 2.5f;
					break;
				case 2L:
					damage2 = 3.34f;
					break;
				case 3L:
					damage2 = 5f;
					break;
				case 4L:
					damage2 = 10f;
					break;
				}
			}
			int debris = 0;
			while (t != null && debrisRandom.NextDouble() < (double)damage2 * 0.08 + (double)((float)t.getLastFarmerToUse().ForagingLevel / 200f))
			{
				debris++;
			}
			this.health.Value -= damage2;
			if (debris > 0)
			{
				Game1.createDebris(12, (int)tileLocation.X, (int)tileLocation.Y, debris, location);
			}
			if (this.health.Value <= 0f)
			{
				if (this.treeId.Value != null)
				{
					Game1.createItemDebris(ItemRegistry.Create("(O)" + this.treeId.Value), tileLocation * 64f, 2, location);
				}
				Game1.createRadialDebris(location, 12, (int)tileLocation.X, (int)tileLocation.Y, Game1.random.Next(20, 30), resource: false);
				return true;
			}
		}
		else if ((int)this.growthStage >= 1)
		{
			if (explosion > 0)
			{
				return true;
			}
			if (t != null && t.BaseName.Contains("Axe"))
			{
				location.playSound("axchop", tileLocation);
				Game1.createRadialDebris(location, 12, (int)tileLocation.X, (int)tileLocation.Y, Game1.random.Next(10, 20), resource: false);
			}
			if (t is Axe || t is Pickaxe || t is Hoe || t is MeleeWeapon)
			{
				Game1.createRadialDebris(location, 12, (int)tileLocation.X, (int)tileLocation.Y, Game1.random.Next(10, 20), resource: false);
				if (t.BaseName.Contains("Axe") && Game1.recentMultiplayerRandom.NextDouble() < (double)((float)t.getLastFarmerToUse().ForagingLevel / 10f))
				{
					Game1.createDebris(12, (int)tileLocation.X, (int)tileLocation.Y, 1, location);
				}
				Game1.multiplayer.broadcastSprites(location, new TemporaryAnimatedSprite(17, tileLocation * 64f, Color.White));
				if (this.treeId.Value != null)
				{
					Game1.createItemDebris(ItemRegistry.Create("(O)" + this.treeId.Value), tileLocation * 64f, 2, location);
				}
				return true;
			}
		}
		else
		{
			if (explosion > 0)
			{
				return true;
			}
			if (t.BaseName.Contains("Axe") || t.BaseName.Contains("Pick") || t.BaseName.Contains("Hoe"))
			{
				location.playSound("woodyHit", tileLocation);
				location.playSound("axchop", tileLocation);
				Game1.multiplayer.broadcastSprites(location, new TemporaryAnimatedSprite(17, tileLocation * 64f, Color.White));
				if (this.treeId.Value != null)
				{
					Game1.createItemDebris(ItemRegistry.Create("(O)" + this.treeId.Value), tileLocation * 64f, 2, location);
				}
				return true;
			}
		}
		return false;
	}

	public override void drawInMenu(SpriteBatch spriteBatch, Vector2 positionOnScreen, Vector2 tileLocation, float scale, float layerDepth)
	{
		layerDepth += positionOnScreen.X / 100000f;
		if ((int)this.growthStage < 4)
		{
			Rectangle sourceRect = this.growthStage switch
			{
				0L => new Rectangle(128, 512, 64, 64), 
				1L => new Rectangle(0, 512, 64, 64), 
				2L => new Rectangle(64, 512, 64, 64), 
				_ => new Rectangle(0, 384, 64, 128), 
			};
			spriteBatch.Draw(this.texture, positionOnScreen - new Vector2(0f, (float)sourceRect.Height * scale), sourceRect, Color.White, 0f, Vector2.Zero, scale, this.flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, layerDepth + (positionOnScreen.Y + (float)sourceRect.Height * scale) / 20000f);
			return;
		}
		if (!this.falling)
		{
			spriteBatch.Draw(this.texture, positionOnScreen + new Vector2(0f, -64f * scale), new Rectangle(128, 384, 64, 128), Color.White, 0f, Vector2.Zero, scale, this.flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, layerDepth + (positionOnScreen.Y + 448f * scale - 1f) / 20000f);
		}
		if (!this.stump || (bool)this.falling)
		{
			spriteBatch.Draw(this.texture, positionOnScreen + new Vector2(-64f * scale, -320f * scale), new Rectangle(0, 0, 192, 384), Color.White, this.shakeRotation, Vector2.Zero, scale, this.flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, layerDepth + (positionOnScreen.Y + 448f * scale) / 20000f);
		}
	}

	public override void draw(SpriteBatch spriteBatch)
	{
		int seasonIndex = Game1.GetSeasonIndexForLocation(this.Location);
		int spriteRow = this.GetSpriteRowNumber();
		Vector2 tileLocation = this.Tile;
		Rectangle boundingBox = this.getBoundingBox();
		if ((bool)this.greenHouseTileTree)
		{
			spriteBatch.Draw(Game1.mouseCursors, Game1.GlobalToLocal(Game1.viewport, new Vector2(tileLocation.X * 64f, tileLocation.Y * 64f)), new Rectangle(669, 1957, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1E-08f);
		}
		if ((int)this.growthStage < 4)
		{
			Vector2 positionOffset = new Vector2((float)Math.Max(-8.0, Math.Min(64.0, Math.Sin((double)(tileLocation.X * 200f) / (Math.PI * 2.0)) * -16.0)), (float)Math.Max(-8.0, Math.Min(64.0, Math.Sin((double)(tileLocation.X * 200f) / (Math.PI * 2.0)) * -16.0))) / 2f;
			Rectangle sourceRect = this.growthStage switch
			{
				0L => new Rectangle(0, spriteRow * 5 * 16, 48, 80), 
				1L => new Rectangle(48, spriteRow * 5 * 16, 48, 80), 
				2L => new Rectangle(96, spriteRow * 5 * 16, 48, 80), 
				_ => new Rectangle(144, spriteRow * 5 * 16, 48, 80), 
			};
			spriteBatch.Draw(this.texture, Game1.GlobalToLocal(Game1.viewport, new Vector2(tileLocation.X * 64f + 32f + positionOffset.X, tileLocation.Y * 64f - (float)sourceRect.Height + 128f + positionOffset.Y)), sourceRect, Color.White, this.shakeRotation, new Vector2(24f, 80f), 4f, this.flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, (float)boundingBox.Bottom / 10000f - tileLocation.X / 1000000f);
		}
		else
		{
			if (!this.stump || (bool)this.falling)
			{
				bool ignoreSeason = this.IgnoresSeasonsHere();
				if (!this.falling)
				{
					spriteBatch.Draw(this.texture, Game1.GlobalToLocal(Game1.viewport, new Vector2(tileLocation.X * 64f + 32f, tileLocation.Y * 64f + 64f)), new Rectangle((12 + (ignoreSeason ? 1 : seasonIndex) * 3) * 16, spriteRow * 5 * 16 + 64, 48, 16), ((int)this.struckByLightningCountdown > 0) ? (Color.Gray * this.alpha) : (Color.White * this.alpha), 0f, new Vector2(24f, 16f), 4f, this.flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 1E-07f);
				}
				spriteBatch.Draw(this.texture, Game1.GlobalToLocal(Game1.viewport, new Vector2(tileLocation.X * 64f + 32f, tileLocation.Y * 64f + 64f)), new Rectangle((12 + (ignoreSeason ? 1 : seasonIndex) * 3) * 16, spriteRow * 5 * 16, 48, 64), ((int)this.struckByLightningCountdown > 0) ? (Color.Gray * this.alpha) : (Color.White * this.alpha), this.shakeRotation, new Vector2(24f, 80f), 4f, this.flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, (float)boundingBox.Bottom / 10000f + 0.001f - tileLocation.X / 1000000f);
			}
			if (this.health.Value >= 1f || (!this.falling && this.health.Value > -99f))
			{
				spriteBatch.Draw(this.texture, Game1.GlobalToLocal(Game1.viewport, new Vector2(tileLocation.X * 64f + 32f + ((this.shakeTimer > 0f) ? ((float)Math.Sin(Math.PI * 2.0 / (double)this.shakeTimer) * 2f) : 0f), tileLocation.Y * 64f + 64f)), new Rectangle(384, spriteRow * 5 * 16 + 48, 48, 32), ((int)this.struckByLightningCountdown > 0) ? (Color.Gray * this.alpha) : (Color.White * this.alpha), 0f, new Vector2(24f, 32f), 4f, this.flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, ((bool)this.stump && !this.falling) ? ((float)boundingBox.Bottom / 10000f) : ((float)boundingBox.Bottom / 10000f - 0.001f - tileLocation.X / 1000000f));
			}
			for (int i = 0; i < this.fruit.Count; i++)
			{
				ParsedItemData obj = (((int)this.struckByLightningCountdown > 0) ? ItemRegistry.GetDataOrErrorItem("(O)382") : ItemRegistry.GetDataOrErrorItem(this.fruit[i].QualifiedItemId));
				Texture2D texture = obj.GetTexture();
				Rectangle sourceRect2 = obj.GetSourceRect();
				switch (i)
				{
				case 0:
					spriteBatch.Draw(texture, Game1.GlobalToLocal(Game1.viewport, new Vector2(tileLocation.X * 64f - 64f + tileLocation.X * 200f % 64f / 2f, tileLocation.Y * 64f - 192f - tileLocation.X % 64f / 3f)), sourceRect2, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, (float)boundingBox.Bottom / 10000f + 0.002f - tileLocation.X / 1000000f);
					break;
				case 1:
					spriteBatch.Draw(texture, Game1.GlobalToLocal(Game1.viewport, new Vector2(tileLocation.X * 64f + 32f, tileLocation.Y * 64f - 256f + tileLocation.X * 232f % 64f / 3f)), sourceRect2, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, (float)boundingBox.Bottom / 10000f + 0.002f - tileLocation.X / 1000000f);
					break;
				case 2:
					spriteBatch.Draw(texture, Game1.GlobalToLocal(Game1.viewport, new Vector2(tileLocation.X * 64f + tileLocation.X * 200f % 64f / 3f, tileLocation.Y * 64f - 160f + tileLocation.X * 200f % 64f / 3f)), sourceRect2, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.FlipHorizontally, (float)boundingBox.Bottom / 10000f + 0.002f - tileLocation.X / 1000000f);
					break;
				}
			}
		}
		foreach (Leaf j in this.leaves)
		{
			spriteBatch.Draw(this.texture, Game1.GlobalToLocal(Game1.viewport, j.position), new Rectangle((24 + seasonIndex) * 16, spriteRow * 5 * 16, 8, 8), Color.White, j.rotation, Vector2.Zero, 4f, SpriteEffects.None, (float)boundingBox.Bottom / 10000f + 0.01f);
		}
	}
}
