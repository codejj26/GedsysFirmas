using FirmasApp.Models;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Data;
using System.IO;

namespace FirmasApp.Services;

/// <summary>
/// Servicio para gestión de base de datos local SQLite
/// Maneja el almacenamiento local de firmas y la cola de sincronización
/// </summary>
public class LocalDbService : IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    private const string DbFileName = "firmas.db";

    public LocalDbService()
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DbFileName);
        _connectionString = $"Data Source={dbPath}";
        InicializarBaseDeDatosAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Inicializa la base de datos y crea las tablas si no existen
    /// </summary>
    private async Task InicializarBaseDeDatosAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Tabla: firmas
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS firmas (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username TEXT NOT NULL UNIQUE,
                    nombre_completo TEXT NOT NULL,
                    firma_data_url TEXT NOT NULL,
                    estado_firma TEXT NOT NULL,
                    fecha_local TEXT NOT NULL,
                    fecha_servidor TEXT,
                    version INTEGER DEFAULT 1,
                    creado_en TEXT NOT NULL,
                    actualizado_en TEXT NOT NULL
                )
            ");

            // Tabla: cola_firmas
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS cola_firmas (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username TEXT NOT NULL,
                    operacion TEXT NOT NULL,
                    firma_data_url TEXT,
                    intentos INTEGER DEFAULT 0,
                    max_intentos INTEGER DEFAULT 5,
                    ultimo_error TEXT,
                    estado TEXT NOT NULL,
                    creado_en TEXT NOT NULL,
                    proximo_intento TEXT
                )
            ");

            // Tabla: sincronizacion
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS sincronizacion (
                    id INTEGER PRIMARY KEY,
                    ultima_sincro TEXT,
                    pendientes INTEGER DEFAULT 0,
                    procesados INTEGER DEFAULT 0,
                    fallidos INTEGER DEFAULT 0,
                    estado TEXT DEFAULT 'Sincronizado',
                    creado_en TEXT
                )
            ");

            // Crear registro único de sincronización si no existe
            var existeSincro = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sincronizacion WHERE id = 1");

            if (existeSincro == 0)
            {
                await connection.ExecuteAsync(
                    "INSERT INTO sincronizacion (id, estado, creado_en) VALUES (1, 'Sincronizado', datetime('now'))");
            }

            // Crear índices para mejor rendimiento
            await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_firmas_username ON firmas(username)");
            await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_firmas_estado ON firmas(estado_firma)");
            await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_cola_username ON cola_firmas(username)");
            await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_cola_estado ON cola_firmas(estado)");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Guarda una firma localmente con el estado especificado
    /// </summary>
    public async Task GuardarAsync(string username, string nombreCompleto, string firmaDataUrl, EstadoFirma estado)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO firmas (username, nombre_completo, firma_data_url, estado_firma, fecha_local, creado_en, actualizado_en)
                VALUES (@username, @nombreCompleto, @firmaDataUrl, @estado, @fechaLocal, datetime('now'), datetime('now'))
                ON CONFLICT(username) DO UPDATE SET
                    nombre_completo = @nombreCompleto,
                    firma_data_url = @firmaDataUrl,
                    estado_firma = @estado,
                    actualizado_en = datetime('now')
            ";

            await connection.ExecuteAsync(sql, new
            {
                username,
                nombreCompleto,
                firmaDataUrl,
                estado = estado.ToString(),
                fechaLocal = DateTime.UtcNow.ToString("O")
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Actualiza el estado de una firma
    /// </summary>
    public async Task ActualizarEstadoAsync(string username, EstadoFirma nuevoEstado)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE firmas
                SET estado_firma = @estado,
                    actualizado_en = datetime('now')
                WHERE username = @username
            ";

            await connection.ExecuteAsync(sql, new
            {
                estado = nuevoEstado.ToString(),
                username
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene una firma por username
    /// </summary>
    public async Task<FirmaLocal?> ObtenerAsync(string username)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT * FROM firmas WHERE username = @username";
            var result = await connection.QueryAsync<FirmaLocal>(sql, new { username });

            return result.FirstOrDefault();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene todas las firmas
    /// </summary>
    public async Task<List<FirmaLocal>> ObtenerTodasAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var result = await connection.QueryAsync<FirmaLocal>("SELECT * FROM firmas ORDER BY creado_en DESC");
            return result.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene firmas en un estado específico
    /// </summary>
    public async Task<List<FirmaLocal>> ObtenerPorEstadoAsync(EstadoFirma estado)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var result = await connection.QueryAsync<FirmaLocal>(
                "SELECT * FROM firmas WHERE estado_firma = @estado ORDER BY creado_en DESC",
                new { estado = estado.ToString() });

            return result.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Elimina una firma localmente
    /// </summary>
    public async Task EliminarAsync(string username)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync("DELETE FROM firmas WHERE username = @username", new { username });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Encola una firma para procesamiento posterior
    /// </summary>
    public async Task EncolarAsync(string username, string firmaDataUrl, string operacion)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO cola_firmas (username, operacion, firma_data_url, estado, creado_en, proximo_intento)
                VALUES (@username, @operacion, @firmaDataUrl, 'Pendiente', datetime('now'), datetime('now'))
            ";

            await connection.ExecuteAsync(sql, new { username, operacion, firmaDataUrl });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene firmas en cola pendientes de procesamiento
    /// </summary>
    public async Task<List<ColaFirma>> ObtenerPendientesAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT * FROM cola_firmas
                WHERE estado = 'Pendiente'
                  AND (proximo_intento IS NULL OR proximo_intento <= datetime('now'))
                ORDER BY creado_en ASC
                LIMIT 10
            ";

            var result = await connection.QueryAsync<ColaFirma>(sql);
            return result.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Marca un item de cola como procesándose
    /// </summary>
    public async Task MarcarProcesandoAsync(int id)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "UPDATE cola_firmas SET estado = 'Procesando' WHERE id = @id",
                new { id });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Elimina un item de la cola después de procesarse exitosamente
    /// </summary>
    public async Task EliminarDeColaAsync(int id)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync("DELETE FROM cola_firmas WHERE id = @id", new { id });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Registra un fallo en el procesamiento de un item de cola
    /// </summary>
    public async Task RegistrarFalloAsync(int id, string error)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE cola_firmas
                SET intentos = intentos + 1,
                    ultimo_error = @error,
                    proximo_intento = datetime('now', '+' || (CAST(30 AS INTEGER) * POWER(2, intentos)) || ' seconds'),
                    estado = CASE WHEN intentos >= max_intentos THEN 'Fallido' ELSE 'Pendiente' END
                WHERE id = @id
            ";

            await connection.ExecuteAsync(sql, new { id, error });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene el estado actual de sincronización
    /// </summary>
    public async Task<Sincronizacion> ObtenerSincronizacionAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var result = await connection.QueryAsync<Sincronizacion>("SELECT * FROM sincronizacion WHERE id = 1");
            return result.First();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Actualiza los contadores de sincronización
    /// </summary>
    public async Task ActualizarSincronizacionAsync(int pendientes, int procesados, int fallidos, string estado)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE sincronizacion
                SET pendientes = @pendientes,
                    procesados = @procesados,
                    fallidos = @fallidos,
                    estado = @estado,
                    ultima_sincro = CASE WHEN @estado = 'Sincronizado' THEN datetime('now') ELSE ultima_sincro END
                WHERE id = 1
            ";

            await connection.ExecuteAsync(sql, new { pendientes, procesados, fallidos, estado });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore?.Dispose();
    }
}
