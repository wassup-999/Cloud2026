using System;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Cloud2026.Services
{
    // Clases para parsear el JSON de la tabla de héroes
    [Serializable]
    public class HeroeStats
    {
        public string id;
        public int life;
        public int attack;
        public int defense;
        public int speed;
    }

    [Serializable]
    public class HeroesRegistry
    {
        public HeroeStats[] heroes;
    }

    // Atributos para la segmentación del paso 8
    public struct AtributosApp
    {
        public string canal;
    }

    public struct AtributosUsuario { }

    public class UGSRemoteConfigService : MonoBehaviour
    {
        // Variables para guardar las estadísticas descargadas
        public int BaseLife { get; private set; }
        public int BaseAttack { get; private set; }
        public float DifficultyMultiplier { get; private set; }
        public HeroesRegistry RegistryOfHeroes { get; private set; }
        public bool IsDownloadedFromCloud { get; private set; }
        public string DatesOrigin { get; private set; }

        public async Task InitializeAndDownload()
        {
            RuntimeConfig config;
            bool fromCloud;
            bool ThereisChache = false;

            try
            {
                // Intenta descargar de la nube (Paso 4)
                config = await RemoteConfigService.Instance.FetchConfigsAsync(new AtributosUsuario(), new AtributosApp { canal = "beta" });
                fromCloud = true;
                DatesOrigin = "Cloud";
                Debug.Log("[RemoteConfig] Downloaded from cloud.");
                Debug.Log($"Connected environment: {config.environmentId}");
            }
            catch (Exception e)
            {
                // If it fails (for example, no internet), use what was left in memory (Step         5)
                Debug.LogWarning($"[RemoteConfig] Cloud did not respond: {e.Message}");
                fromCloud = false;
                config = RemoteConfigService.Instance.appConfig;
                ThereisChache = config.GetKeys().Length > 0;
                DatesOrigin = ThereisChache ? "Cache" : "Default values";
                Debug.Log(ThereisChache ? "[RemoteConfig] Using cache." : "[RemoteConfig] No cache, using default values.");
            }

            IsDownloadedFromCloud = fromCloud;

            // Extract variables (with default values)
            BaseLife = config.GetInt("base_life", 100);
            BaseAttack = config.GetInt("base_attack", 10);
            DifficultyMultiplier = config.GetFloat("difficulty_multiplier", 1f);

            // Parse the JSON
            if (config.HasKey("heroes_registry"))
            {
                RegistryOfHeroes = JsonUtility.FromJson<HeroesRegistry>(config.GetJson("heroes_registry", "{}"));
                Debug.Log($"[RemoteConfig] Heroes table loaded. Found: {RegistryOfHeroes?.heroes?.Length}");
            }

            Debug.Log($"[RemoteConfig] Current values - Life: {BaseLife}, Attack: {BaseAttack}");
        }

        // Method for units to request their stats (Step 6)
        public HeroeStats SearchHeroe(string idHeroe)
        {
            if (RegistryOfHeroes == null || RegistryOfHeroes.heroes == null) return null;

            foreach (var heroe in RegistryOfHeroes.heroes)
            {
                if (heroe.id == idHeroe) return heroe;
            }
            return null;
        }
    }
}