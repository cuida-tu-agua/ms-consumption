# ms-consumption (consumption-service · :3004)

Consumo de agua por lugar (HU-015 día, HU-016 semana, HU-017 mes, HU-018 caudal actual) y meta mensual por lugar (HU-023 definir/editar, HU-024 avance).
.NET 10 · arquitectura hexagonal · SQL Server (schemas `monitoring` y `goals`, los crea **ms-consumption-db** v1.1) · RabbitMQ.

## Cómo le llegan los datos
ESP32 → MQTT → **ms-devices** → RabbitMQ `device.reading.received` → cola `consumption.readings` → este servicio.
Guarda cada lectura (`monitoring.readings`, sin duplicados) y suma por hora (`monitoring.consumption_hourly`).

## Endpoints
Todos con JWT de ms-iam; ms-places valida que el lugar sea tuyo (si no, 404). `tz` es opcional (por defecto `America/Bogota`).

| Método y ruta | Qué hace |
|---|---|
| `GET /api/places/{placeId}/consumption?period=day\|week\|month&tz=…` | Consumo del periodo (HU-015..018) |
| `GET /api/places/{placeId}/goals/current?tz=…` | HU-024: meta del mes actual (local) y su avance. Sin meta: `hasGoal=false` y los litros del mes |
| `PUT /api/places/{placeId}/goals/current?tz=…` con `{"targetLiters": 12000}` | HU-023: crea o edita la meta del mes actual. Responde lo mismo que el GET |

La meta siempre va en **litros** (la app convierte a m³ o galones). `status`: `ON_TRACK` (< 80 %), `WARNING` (≥ 80 %), `EXCEEDED` (≥ 100 %). `percentage` se trunca a 1 decimal. Una meta inválida (falta, ≤ 0 o > 10.000.000 L) responde 400 `goal.invalid_target`. Los meses pasados no se editan: la API solo llega al mes actual.

## Puesta en marcha
```powershell
dotnet user-secrets set "ConnectionStrings:Consumption" "Server=localhost,1433;Database=sy-water-db;User Id=consumption_app;Password=<...>;TrustServerCertificate=True" --project src/SyWater.Consumption.Api
dotnet user-secrets set "RabbitMq:Password" "<RABBITMQ_PASSWORD>" --project src/SyWater.Consumption.Api
Copy-Item ..\ms-iam\keys\public.pem src\SyWater.Consumption.Api\keys\iam-public.pem -Force
dotnet test
dotnet run --project src/SyWater.Consumption.Api
```
Sin `RabbitMq:Host` el servicio arranca pero no recibe lecturas.
