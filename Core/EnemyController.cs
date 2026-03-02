using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TiledLib;

namespace VoxelShooter
{
	public enum EnemyType
	{
		Asteroid,
        Omega,
        Turret,
        Squid
	}

	public class EnemyController
	{
		public static EnemyController Instance;

		public List<Enemy> Enemies = new List<Enemy>();

        List<MapObject> Spawns = new List<MapObject>();

        List<Wave> Waves = new List<Wave>();

		Dictionary<string, VoxelSprite> spriteSheets = new Dictionary<string,VoxelSprite>();

		GraphicsDevice graphicsDevice;
		BasicEffect drawEffect;

		public EnemyController(GraphicsDevice gd)
		{
			Instance = this;

			graphicsDevice = gd;

			drawEffect = new BasicEffect(gd)
			{
				VertexColorEnabled = true
			};
		}

		public void LoadContent(ContentManager content, MapObjectLayer spawnLayer)
		{
			VoxelSprite asteroid = new VoxelSprite(16,16,16);
            BvxLoader.LoadSprite(Path.Combine(content.RootDirectory, "enemies", "asteroids.bvx"), ref asteroid);
			spriteSheets.Add("Asteroid", asteroid);
            VoxelSprite omega = new VoxelSprite(15,15,15);
            BvxLoader.LoadSprite(Path.Combine(content.RootDirectory, "enemies", "omega.bvx"), ref omega);
            spriteSheets.Add("Omega", omega);
            VoxelSprite turret = new VoxelSprite(15, 15, 15);
            BvxLoader.LoadSprite(Path.Combine(content.RootDirectory, "enemies", "turret.bvx"), ref turret);
            spriteSheets.Add("Turret", turret);
            VoxelSprite squid = new VoxelSprite(15, 15, 15);
            BvxLoader.LoadSprite(Path.Combine(content.RootDirectory, "enemies", "squid.bvx"), ref squid);
            spriteSheets.Add("Squid", squid);

            // [SFX-EXPLODE] Enemy.SfxExplosion = content.Load<Microsoft.Xna.Framework.Audio.SoundEffect>("Sound/explosion");

            foreach (MapObject o in spawnLayer.Objects) Spawns.Add(o);
		}

		public Enemy Spawn(EnemyType type, Vector3 pos, PropertyCollection props)
		{
            Enemy e = null;
			switch (type)
			{
				case EnemyType.Asteroid:
                    e = new Asteroid(pos, spriteSheets["Asteroid"]);
				    break;
                case EnemyType.Omega:
                    e = new Omega(pos, spriteSheets["Omega"]);
                    break;
                case EnemyType.Turret:
                    e = new Turret(pos, spriteSheets["Turret"], props.Contains("Inverted"));
                    break;
                case EnemyType.Squid:
                    e = new Squid(pos, spriteSheets["Squid"]);
                    break;
			}

            // Turrets are stationary; skip Bepu for now (handled in a future pass).
            if (type != EnemyType.Turret && PhysicsManager.Instance != null)
                e.InitPhysics(PhysicsManager.Instance);

            Enemies.Add(e);
            return e;
		}

		
		public void Update(GameTime gameTime, ICamera gameCamera, Hero gameHero, VoxelWorld gameWorld, float scrollPos, float scrollSpeed)
		{
            for(int i=Spawns.Count-1;i>=0;i--)
            {
                if (gameWorld.ToScreenSpace(Spawns[i].Location.Center.X, Spawns[i].Location.Center.Y, 5).X < (int)scrollPos + 75)
                {
                    if (Spawns[i].Properties.Contains("IsWave"))
                    {
                        Wave w;
                        switch (Spawns[i].Properties["IsWave"])
                        {
                            case "Line":
                                w = new Wave(gameWorld.ToScreenSpace(Spawns[i].Location.Center.X, Spawns[i].Location.Center.Y, 10), WaveType.Line, (EnemyType)Enum.Parse(typeof(EnemyType), Spawns[i].Name), Convert.ToInt16(Spawns[i].Properties["Count"]), Spawns[i].Properties);
                                
                                break;
                            default:
                                w = new Wave(gameWorld.ToScreenSpace(Spawns[i].Location.Center.X, Spawns[i].Location.Center.Y, 10), WaveType.Circle, (EnemyType)Enum.Parse(typeof(EnemyType), Spawns[i].Name), Convert.ToInt16(Spawns[i].Properties["Count"]), Spawns[i].Properties);
                                
                                break;
                        }
                        Waves.Add(w);
                    }
                    else
                    {
                        Spawn((EnemyType)Enum.Parse(typeof(EnemyType), Spawns[i].Name), gameWorld.ToScreenSpace(Spawns[i].Location.Center.X, Spawns[i].Location.Center.Y, 10), Spawns[i].Properties);
                    }
                    Spawns.RemoveAt(i);
                }
            }

			for(int i=Enemies.Count-1;i>=0;i--) Enemies[i].Update(gameTime, gameWorld, gameHero);

            // Destroy physics bodies for enemies scrolled off-screen (Die() handles the health=0 case).
            if (PhysicsManager.Instance != null)
                foreach (var en in Enemies)
                    if (en.Active && en.Position.X < scrollPos - 110f)
                        en.DestroyPhysics(PhysicsManager.Instance);

			Enemies.RemoveAll(en => !en.Active || en.Position.X<scrollPos-110f);

            foreach (Wave w in Waves) w.Update(gameTime, scrollSpeed);

            // Wave.Update() assigns e.Position directly; sync those positions back to Bepu
            // bodies so that Enemy.Update() reads the correct formation position next frame.
            if (PhysicsManager.Instance != null)
                foreach (var en in Enemies)
                    en.SyncPhysicsToPosition();

			drawEffect.World = gameCamera.WorldMatrix;
			drawEffect.View = gameCamera.ViewMatrix;
			drawEffect.Projection = gameCamera.ProjectionMatrix;
		}

		public void Draw(ICamera gameCamera)
		{

			foreach (Enemy e in Enemies)
			{
				drawEffect.DiffuseColor = new Vector3(1f,1f-e.hitAlpha,1f-e.hitAlpha);
				drawEffect.Alpha = 1f;
				drawEffect.World = gameCamera.WorldMatrix *
					Matrix.CreateRotationX(e.Rotation.X) *
                        Matrix.CreateRotationY(e.Rotation.Y) *
						Matrix.CreateRotationZ(e.Rotation.Z) *
						Matrix.CreateScale(e.Scale) *
						Matrix.CreateTranslation(e.Position);

                foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();


                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, e.spriteSheet.AnimChunks[e.CurrentFrame + e.offsetFrame].VertexArray, 0, e.spriteSheet.AnimChunks[e.CurrentFrame + e.offsetFrame].VertexArray.Length, e.spriteSheet.AnimChunks[e.CurrentFrame + e.offsetFrame].IndexArray, 0, e.spriteSheet.AnimChunks[e.CurrentFrame + e.offsetFrame].VertexArray.Length / 2);

                }

			}

            foreach (Enemy e in Enemies.Where(en => en is Turret))
            {
                drawEffect.DiffuseColor = new Vector3(1f, 1f - e.hitAlpha, 1f - e.hitAlpha);
                drawEffect.Alpha = 1f;
                drawEffect.World = gameCamera.WorldMatrix *
                    Matrix.CreateRotationX(e.Rotation.X + (((Turret)e).Inverted ? MathHelper.Pi : 0f)) *
                    Matrix.CreateTranslation(new Vector3(0, ((Turret)e).Inverted?4f:-3f, 0)) *
                        Matrix.CreateRotationZ(e.Rotation.Z + (((Turret)e).barrelRot + MathHelper.PiOver2)) *
                        
                        Matrix.CreateRotationY(e.Rotation.Y) *
                        //Matrix.CreateRotationZ(e.Rotation.Z) *
                        Matrix.CreateScale(e.Scale) *
                        Matrix.CreateTranslation(e.Position + new Vector3(0, ((Turret)e).Inverted ? -4f : 3f, 0));
                        

                foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();


                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, e.spriteSheet.AnimChunks[1].VertexArray, 0, e.spriteSheet.AnimChunks[1].VertexArray.Length, e.spriteSheet.AnimChunks[1].IndexArray, 0, e.spriteSheet.AnimChunks[1].VertexArray.Length / 2);

                }
            }
		}


	}
}