# ms-consumption (consumption-service · :3004)

Consumo de agua por lugar (HU-015 día, HU-016 semana, HU-017 mes, HU-018 caudal actual).
.NET 10 · arquitectura hexagonal · SQL Server (schema `monitoring`, lo crea **ms-consumption-db**) · RabbitMQ.

## Cómo le llegan los datos
ESP32 → MQTT → **ms-devices** → RabbitMQ `device.reading.received` → cola `consumption.readings` → este servicio.
Guarda cada lectura (`monitoring.readings`, sin duplicados) y suma por hora (`monitoring.consumption_hourly`).

## Endpoint
`GET /api/places/{placeId}/consumption?period=day|week|month&tz=America/Bogota` (JWT de ms-iam; ms-places valida que el lugar sea tuyo).

## Puesta en marcha
```powershell
dotnet user-secrets set "ConnectionStrings:Consumption" "Server=localhost,1433;Database=sy-water-db;User Id=consumption_app;Password=<...>;TrustServerCertificate=True" --project src/SyWater.Consumption.Api
dotnet user-secrets set "RabbitMq:Password" "<RABBITMQ_PASSWORD>" --project src/SyWater.Consumption.Api
Copy-Item ..\ms-iam\keys\public.pem src\SyWater.Consumption.Api\keys\iam-public.pem -Force
dotnet test
dotnet run --project src/SyWater.Consumption.Api
```
Sin `RabbitMq:Host` el servicio arranca pero no recibe lecturas.
