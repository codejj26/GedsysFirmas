# 📚 Documentación de FirmasApp

Esta carpeta contiene la documentación técnica y guías de implementación del sistema de gestión de firmas biométricas.

## 📋 Documentos Disponibles

### **🔑 CONFIGURACION_KEYCLOAK.md**
Guía completa de configuración de autenticación Keycloak para la aplicación.
- Configuración de Realm y Client
- Endpoints OAuth/OIDC
- Flujos de autenticación
- Troubleshooting común

### **🖼️ LOGOS_GUIDE.md**
Guía para agregar y personalizar logos/iconos de la aplicación.
- Formatos de imágenes soportados
- Ubicación de archivos de recursos
- Integración en WPF

### **📄 PAGINACION.md**
Documentación de la implementación de paginación de usuarios.
- Configuración de páginas
- Botones de navegación
- Integración con API
- Optimización de rendimiento

### **🔄 PLAN_COLA_FIRMAS.md**
Plan técnico completo del sistema offline-first para gestión de firmas.
- Arquitectura de cola de sincronización
- Base de datos SQLite local
- Servicios de sincronización
- Estrategia de reintentos con backoff exponencial
- Fases de implementación (SQLite → Queue → Sync)

### **🖊️ WACOM_IMPLEMENTATION_SUMMARY.md**
Resumen técnico de la implementación de captura biométrica con tablet Wacom STU-430.
- Integración con SDK Wacom
- P/Invoke declarations
- Gestión de callbacks
- Configuración de presión y sensibilidad
- Manejo de eventos de captura

## 📖 Uso de la Documentación

**Para desarrolladores nuevos:**
1. Lee `PLAN_COLA_FIRMAS.md` primero para entender la arquitectura
2. Revisa `CONFIGURACION_KEYCLOAK.md` para configurar autenticación
3. Consulta `WACOM_IMPLEMENTATION_SUMMARY.md` para entender captura biométrica

**Para configuración y deployment:**
1. `CONFIGURACION_KEYCLOAK.md` - Autenticación
2. `LOGOS_GUIDE.md` - Branding
3. `PAGINACION.md` - Performance

**Para troubleshooting:**
- Consulta los documentos específicos del área problemática
- Revisa los logs de aplicación para más detalles

## 🔄 Mantenimiento

Los documentos se actualizan conforme se implementan nuevas características:
- ✅ Fase 1: Base de datos SQLite (COMPLETADO)
- ✅ Fase 2: Sistema de colas (COMPLETADO)
- ✅ Fase 3: Coordinador de sincronización (COMPLETADO)
- ✅ Sincronización Cloud Supabase (COMPLETADO)

## 📝 Convenciones

- **Código**: `código` o bloques de código
- **Archivos**: `nombre_archivo.ext`
- **Comandos**: `comando`
- **Rutas**: `/ruta/al/archivo`
- **Énfasis**: *texto importante*
- **Advertencias**: ⚠️ **texto de advertencia**

---
**Última actualización**: Julio 2026
**Versión**: 1.0.0