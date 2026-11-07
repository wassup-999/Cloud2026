using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.RemoteConfig;

namespace Cloud2026.Services
{
    // --- CLASES DEL CATÁLOGO (JSON de Remote Config) ---
    [Serializable] public class MonedaCatalogo { public string id; public int inicial; }
    [Serializable] public class CatalogoEconomia { public MonedaCatalogo[] monedas; }

    // --- CLASES DE CLOUD SAVE ---
    [Serializable] public class MonedaSaldo { public string id; public int cantidad; }
    [Serializable] public class SaldosJugador { public int version = 1; public List<MonedaSaldo> monedas = new List<MonedaSaldo>(); }
    [Serializable] public class InventarioJugador { public int version = 1; public List<string> objetos = new List<string>(); public List<string> heroes = new List<string>(); }
    [Serializable] public class CompraCatalogo { public string id; public string cuestaMoneda; public int cuestaCantidad; public string entregaItem; }
    [Serializable] public class CatalogoEconomiaParaCompra { public List<CompraCatalogo> compras; }
    public class UGSEconomiaService : MonoBehaviour
    {
        public SaldosJugador Saldos { get; private set; }
        public InventarioJugador Inventario { get; private set; }
        public Dictionary<string, string> Cerrojos { get; private set; } = new Dictionary<string, string>();

        public async Task CargarEconomia()
        {
            // 1. Leer el catálogo que ya se descargó en el paso anterior
            var config = RemoteConfigService.Instance.appConfig;
            CatalogoEconomia catalogo = null;
            if (config.HasKey("catalog_economy"))
            {
                catalogo = JsonUtility.FromJson<CatalogoEconomia>(config.GetJson("catalog_economy", "{}"));
            }

            // 2. Pedir saldos e inventario a la nube (Paso 5 y 6)
            var llaves = new HashSet<string> { "salary", "inventory" };
            var datos = await CloudSaveService.Instance.Data.Player.LoadAsync(llaves);

            // Guardar los write locks para la compra segura del Paso 7
            Cerrojos.Clear();
            foreach (var par in datos)
            {
                Cerrojos[par.Key] = par.Value.WriteLock;
            }

            // 3. Procesar Saldos
            if (datos.TryGetValue("salary", out var itemSaldos))
            {
                Saldos = itemSaldos.Value.GetAs<SaldosJugador>();
                Debug.Log($"[Economia] Saldos cargados desde la nube.");
            }
            else
            {
                // Jugador nuevo: crear saldo inicial desde el catálogo
                Saldos = new SaldosJugador();
                if (catalogo != null && catalogo.monedas != null)
                {
                    foreach (var m in catalogo.monedas)
                    {
                        Saldos.monedas.Add(new MonedaSaldo { id = m.id, cantidad = m.inicial });
                    }
                }

                // Guardar el nuevo saldo en la nube
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { "salary", Saldos } });
                Debug.Log("[Economia] Jugador nuevo: saldo inicial creado desde el catálogo y guardado en la nube.");
            }

            // 4. Procesar Inventario
            if (datos.TryGetValue("inventory", out var itemInv))
            {
                Inventario = itemInv.Value.GetAs<InventarioJugador>();
                Debug.Log($"[Economia] Inventario cargado. objetos: {Inventario.objetos.Count}, heroes: {Inventario.heroes.Count}");
            }
            else
            {
                Inventario = new InventarioJugador();
                Debug.Log("[Economia] Inventario al iniciar sesión: VACÍO (0 ítems, no es un error).");
            }
        }
        public async Task<bool> RealizarCompra(string idCompra)
        {
            // 1. Leer el catálogo de Remote Config para buscar la regla de la compra
            var config = RemoteConfigService.Instance.appConfig;
            if (!config.HasKey("catalog_economy")) return false;

            // Parseo rápido para encontrar la compra
            var catalogo = JsonUtility.FromJson<CatalogoEconomiaParaCompra>(config.GetJson("catalog_economy", "{}"));
            CompraCatalogo compra = null;
            if (catalogo != null && catalogo.compras != null)
            {
                foreach (var c in catalogo.compras)
                {
                    if (c.id == idCompra) { compra = c; break; }
                }
            }
            if (compra == null) return false;

            // 2. Buscar el saldo actual de la moneda requerida
            var monedaSaldo = Saldos.monedas.Find(m => m.id == compra.cuestaMoneda);
            int saldoActual = monedaSaldo != null ? monedaSaldo.cantidad : 0;

            // Validación de fondos insuficientes (Paso 7 y 8)
            if (saldoActual < compra.cuestaCantidad)
            {
                Debug.LogWarning($"[Economia] COMPRA RECHAZADA: Te faltan {compra.cuestaCantidad - saldoActual} de {compra.cuestaMoneda} para {compra.id} (tienes {saldoActual}, cuesta {compra.cuestaCantidad}).");
                return false;
            }

            // 3. Aplicar el cobro y entregar el ítem sobre copias locales
            monedaSaldo.cantidad -= compra.cuestaCantidad;

            if (compra.entregaItem.StartsWith("HEROE_"))
                Inventario.heroes.Add(compra.entregaItem);
            else
                Inventario.objetos.Add(compra.entregaItem);

            // 4. Guardar en Cloud Save usando UNA SOLA llamada con ambos cerrojos (Write Locks) (Paso 7)
            var datosGuardar = new Dictionary<string, SaveItem>
            {
                { "salary", new SaveItem(Saldos, Cerrojos.ContainsKey("salary") ? Cerrojos["salary"] : null) },
                { "inventory", new SaveItem(Inventario, Cerrojos.ContainsKey("inventory") ? Cerrojos["inventory"] : null) }
            };

            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(datosGuardar);
                Debug.Log($"[Economia] ¡COMPRA EXITOSA! Compraste {compra.entregaItem} por {compra.cuestaCantidad} de {compra.cuestaMoneda}.");

                // Actualizar cerrojos tras el guardado exitoso
                var llaves = new HashSet<string> { "salary", "inventory" };
                var nuevosDatos = await CloudSaveService.Instance.Data.Player.LoadAsync(llaves);
                foreach (var par in nuevosDatos) Cerrojos[par.Key] = par.Value.WriteLock;

                return true;
            }
            catch (CloudSaveConflictException)
            {
                Debug.LogWarning("[Economia] CONFLICTO: Otro dispositivo modificó los datos. La compra fue rechazada.");
                await CargarEconomia(); // Recargar el estado real de la nube
                return false;
            }
        }
        public void ComprarGuerrero()
        {
            Debug.Log("Botón Guerrero presionado. Intentando compra...");
            _ = RealizarCompra("COMPRA_GUERRERO");
        }

        public void ComprarCajaBasica()
        {
            Debug.Log("Botón Caja Básica presionado. Intentando compra...");
            _ = RealizarCompra("COMPRA_CAJA_BASICA");
        }

    }
}