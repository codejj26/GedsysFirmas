using FirmasApp.Models;
using Npgsql;
using Dapper;

namespace FirmasApp.Services;

/// <summary>
/// Servicio para gestión de base de datos PostgreSQL en la nube (Supabase)
/// Sincroniza datos con SQLite local para backup y multi-dispositivo
/// </summary>
public class CloudDbService : IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    public CloudDbService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <summary>
    /// Prueba la conexión con la base de datos cloud
    /// </summary>
    public async Task<bool> ProbarConexionAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Inicializa las tablas en PostgreSQL si no existen
    /// </summary>
    public async Task InicializarTablasAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Tabla: firmas (equivalente a SQLite)
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS firmas (
                    id SERIAL PRIMARY KEY,
                    username VARCHAR(255) NOT NULL UNIQUE,
                    nombre_completo VARCHAR(500) NOT NULL,
                    firma_data_url TEXT NOT NULL,
                    estado_firma VARCHAR(50) NOT NULL,
                    fecha_local TIMESTAMP NOT NULL,
                    fecha_servidor TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    version INTEGER DEFAULT 1,
                    creado_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    actualizado_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ");

            // Tabla: sincronizacion (registro global de sincro)
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS sincronizacion (
                    id INTEGER PRIMARY KEY,
                    ultima_sincro TIMESTAMP,
                    pendientes INTEGER DEFAULT 0,
                    procesados INTEGER DEFAULT 0,
                    fallidos INTEGER DEFAULT 0,
                    estado VARCHAR(100) DEFAULT 'Sincronizado',
                    creado_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )
            ");

            // Crear registro único de sincronización si no existe
            var existeSincro = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sincronizacion WHERE id = 1");

            if (existeSincro == 0)
            {
                await connection.ExecuteAsync(
                    "INSERT INTO sincronizacion (id, estado) VALUES (1, 'Sincronizado')");
            }

            // Crear índices para mejor rendimiento
            await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_firmas_username ON firmas(username)");
            await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_firmas_estado ON firmas(estado_firma)");

            AppLog.Info("CloudDb", "Tablas de PostgreSQL inicializadas correctamente");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene todas las firmas desde PostgreSQL
    /// </summary>
    public async Task<List<FirmaLocal>> ObtenerTodasFirmasAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var firmas = await connection.QueryAsync<FirmaLocal>(
                "SELECT * FROM firmas ORDER BY fecha_servidor DESC");

            return firmas.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene una firma específica por username
    /// </summary>
    public async Task<FirmaLocal?> ObtenerFirmaAsync(string username)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var firma = await connection.QuerySingleOrDefaultAsync<FirmaLocal>(
                "SELECT * FROM firmas WHERE username = @Username", new { Username = username });

            return firma;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Inserta o actualiza una firma en PostgreSQL (UPSERT)
    /// </summary>
    public async Task GuardarFirmaAsync(FirmaLocal firma)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO firmas (username, nombre_completo, firma_data_url, estado_firma, fecha_local, version)
                VALUES (@Username, @NombreCompleto, @FirmaDataUrl, @EstadoFirma, @FechaLocal, @Version)
                ON CONFLICT (username)
                DO UPDATE SET
                    nombre_completo = EXCLUDED.nombre_completo,
                    firma_data_url = EXCLUDED.firma_data_url,
                    estado_firma = EXCLUDED.estado_firma,
                    version = EXCLUDED.version + 1,
                    actualizado_en = CURRENT_TIMESTAMP
                RETURNING id, fecha_servidor, actualizado_en";

            await connection.ExecuteAsync(sql, firma);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Elimina una firma de PostgreSQL
    /// </summary>
    public async Task EliminarFirmaAsync(string username)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "DELETE FROM firmas WHERE username = @Username", new { Username = username });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene el estado de sincronización desde PostgreSQL
    /// </summary>
    public async Task<SincronizacionInfo> ObtenerSincronizacionAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var sincro = await connection.QuerySingleOrDefaultAsync<SincronizacionInfo>(
                "SELECT * FROM sincronizacion WHERE id = 1");

            return sincro ?? new SincronizacionInfo { Id = 1, Estado = "Sincronizado" };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Actualiza el estado de sincronización en PostgreSQL
    /// </summary>
    public async Task ActualizarSincronizacionAsync(int pendientes, int procesados, int fallidos, string estado)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(@"
                UPDATE sincronizacion
                SET pendientes = @Pendientes,
                    procesados = @Procesados,
                    fallidos = @Fallidos,
                    estado = @Estado,
                    ultima_sincro = CURRENT_TIMESTAMP
                WHERE id = 1",
                new { Pendientes = pendientes, Procesados = procesados, Fallidos = fallidos, Estado = estado });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene firmas modificadas después de una fecha específica (para sincronización incremental)
    /// </summary>
    public async Task<List<FirmaLocal>> ObtenerFirmasModificadasAsync(DateTime desde)
    {
        await _semaphore.WaitAsync();
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var firmas = await connection.QueryAsync<FirmaLocal>(
                "SELECT * FROM firmas WHERE actualizado_en > @Desde ORDER BY actualizado_en ASC",
                new { Desde = desde });

            return firmas.ToList();
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

/// <summary>
/// Información de sincronización
/// </summary>
public class SincronizacionInfo
{
    public int Id { get; set; }
    public DateTime? UltimaSincro { get; set; }
    public int Pendientes { get; set; }
    public int Procesados { get; set; }
    public int Fallidos { get; set; }
    public string Estado { get; set; } = string.Empty;
}