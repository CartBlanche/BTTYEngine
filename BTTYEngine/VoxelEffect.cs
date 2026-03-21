using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BTTYEngine
{
    public class VoxelEffect
    {
        public readonly Effect Effect;

        // Sun
        private readonly EffectParameter _sunDirection;
        private readonly EffectParameter _sunColor;
        private readonly EffectParameter _ambientColor;

        // Point lights
        private const int MaxPointLights = 8;
        private readonly EffectParameter _plPosition;
        private readonly EffectParameter _plColor;
        private readonly EffectParameter _plRadius;
        private readonly EffectParameter _plIntensity;
        private readonly Vector3[] _plPosStaging  = new Vector3[MaxPointLights];
        private readonly Vector3[] _plColStaging  = new Vector3[MaxPointLights];
        private readonly float[]   _plRadStaging  = new float[MaxPointLights];
        private readonly float[]   _plIntStaging  = new float[MaxPointLights];

        // Transforms
        private readonly EffectParameter _tint;
        private readonly EffectParameter _world;
        private readonly EffectParameter _worldViewProj;
        private readonly EffectParameter _worldInverseTranspose;

        public VoxelEffect(Effect effect)
        {
            Effect                 = effect;
            _sunDirection          = effect.Parameters["SunDirection"];
            _sunColor              = effect.Parameters["SunColor"];
            _ambientColor          = effect.Parameters["AmbientColor"];
            _plPosition            = effect.Parameters["PointLightPosition"];
            _plColor               = effect.Parameters["PointLightColor"];
            _plRadius              = effect.Parameters["PointLightRadius"];
            _plIntensity           = effect.Parameters["PointLightIntensity"];
            _tint                  = effect.Parameters["Tint"];
            _world                 = effect.Parameters["World"];
            _worldViewProj         = effect.Parameters["WorldViewProj"];
            _worldInverseTranspose = effect.Parameters["WorldInverseTranspose"];
            SetTint(Vector3.One);
        }

        public void SetSun(Vector3 direction, Color sunColor, Color ambientColor)
        {
            _sunDirection.SetValue(direction);
            _sunColor.SetValue(sunColor.ToVector3());
            _ambientColor.SetValue(ambientColor.ToVector3());
        }

        // Draw geometry as fully self-illuminated (emissive) — ignores sun and ambient.
        // Call before drawing projectiles, particles, or any other glowing objects.
        public void SetFullbright() => SetSun(Vector3.Zero, Color.Black, Color.White);

        public void SetPointLight(int index, Vector3 position, Color color, float radius, float intensity)
        {
            _plPosStaging[index] = position;
            _plColStaging[index] = color.ToVector3();
            _plRadStaging[index] = radius;
            _plIntStaging[index] = intensity;
        }

        public void CommitPointLights(int count)
        {
            // Zero intensity for unused slots so the shader's fixed loop ignores them.
            for (int i = count; i < MaxPointLights; i++)
                _plIntStaging[i] = 0f;
            _plPosition.SetValue(_plPosStaging);
            _plColor.SetValue(_plColStaging);
            _plRadius.SetValue(_plRadStaging);
            _plIntensity.SetValue(_plIntStaging);
        }

        public void SetTint(Vector3 tint) => _tint.SetValue(tint);

        public void Apply(Matrix world, Matrix view, Matrix projection)
        {
            var wvp = world * view * projection;
            _worldViewProj.SetValue(wvp);
            _world.SetValue(world);

            Matrix.Invert(ref world, out Matrix worldInvTranspose);
            Matrix.Transpose(ref worldInvTranspose, out worldInvTranspose);
            _worldInverseTranspose.SetValue(worldInvTranspose);

            Effect.CurrentTechnique.Passes[0].Apply();
        }
    }
}
