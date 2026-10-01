using UnityEngine;
using TMPro;
using Cloud2026.Services;

namespace Cloud2026
{
    public class RemoteConfigUI : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private UGSRemoteConfigService remoteConfigService;
        [SerializeField] private TextMeshProUGUI OriginText;
        [SerializeField] private TextMeshProUGUI StatsText;

        [Header("Configuración")]
        [SerializeField] private string ShownId = "guerrero";

        void Start()
        {
            UploadScreen();
        }

        public void UploadScreen()
        {
            if (remoteConfigService == null) return;

            // Muestra el origen en la esquina (Requisito del paso 6)
            OriginText.text = $"Balance: {remoteConfigService.DatesOrigin}";

            // Muestra los Valores Base (aquí se reflejará tu 99)
            string textoFinal = $" base values \n" +
                                $"Base Life: {remoteConfigService.BaseLife}\n" +
                                $"Base Attack: {remoteConfigService.BaseAttack}\n\n";

            // Muestra también los datos del JSON
            var heroe = remoteConfigService.SearchHeroe(ShownId);
            if (heroe != null)
            {
                textoFinal += $"registry\n" +
                              $"Unit: {heroe.id.ToUpper()}\n" +
                              $"Life: {heroe.life} | Attack: {heroe.attack} | Def: {heroe.defense}";
            }

            StatsText.text = textoFinal;
        }
        public async void UploadFromCloud()
        {
            Debug.Log("Refreshing data from the cloud...");
            await remoteConfigService.InitializeAndDownload();
            UploadScreen();
        }
    }
}