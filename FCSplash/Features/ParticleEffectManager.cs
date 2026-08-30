using System;
using UnityEngine;

namespace FCSplash.Features;

public class ParticleEffectManager
{
    public void TriggerSparkleEffect(Vector3 position)
    {
        try
        {
            GameObject sparkleObj = new GameObject("FC_SparkleParticles");
            sparkleObj.transform.position = position;

            ParticleSystem ps = sparkleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2.0f;
            main.startLifetime = 3.5f;
            main.startSpeed = 5.0f;
            main.startSize = 0.06f;
            main.maxParticles = 300;
            main.loop = false;
            main.gravityModifier = 0.5f;

            var cfg = Config.Instance.Particles;
            Color32 sparkleColor = new Color32(
                (byte)cfg.SparkleColorR, 
                (byte)cfg.SparkleColorG, 
                (byte)cfg.SparkleColorB, 
                255
            );
            main.startColor = new ParticleSystem.MinMaxGradient(sparkleColor);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 200) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var renderer = sparkleObj.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Shader? shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                
                if (shader != null)
                {
                    renderer.material = new Material(shader);
                }
            }

            ps.Play();

            UnityEngine.Object.Destroy(sparkleObj, 5.0f);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"ParticleEffectManager: Failed to create particle effect: {ex.Message}");
        }
    }
}