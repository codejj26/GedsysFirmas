# Configuración de Keycloak - FirmasApp

## Descripción general

La aplicación ahora incluye una funcionalidad que permite cambiar la configuración de Keycloak antes de iniciar sesión. Esto es útil cuando necesitas cambiar entre diferentes entornos (desarrollo, producción) o cuando la URL de Keycloak cambia.

## Características

- **Interfaz de configuración fácil de usar**: Dialogo intuitivo para modificar la configuración de Keycloak
- **Validación de datos**: Verifica que la URL sea válida y que los campos obligatorios estén completos
- **Notas informativas**: Instrucciones claras sobre cómo debe estar configurada cada campo
- **Información de configuración actual**: Muestra la configuración actual antes de realizar cambios
- **Restablecimiento de valores**: Permite volver a los valores originales si se equivoca

## Cómo acceder a la configuración

### Desde la ventana principal

1. En la ventana principal de la aplicación, haz clic en el botón **"⚙️ Configurar"**
2. Se abrirá el diálogo de configuración de Keycloak

### Desde la ventana de login

1. Si no estás autenticado, también puedes acceder a la configuración desde la ventana de login
2. Haz clic en el botón **"⚙️ Configurar Keycloak"**

## Campos de configuración

### URL del Servidor Keycloak
- **Descripción**: La URL base del servidor de Keycloak
- **Formato**: Debe incluir el protocolo (https://)
- **Ejemplos**:
  - Desarrollo: `https://keycloak.gedsys.co`
  - Producción: `https://keycloak.production.gedsys.co`
- **Validación**: La aplicación verifica que sea una URL válida

### Realm
- **Descripción**: El realm de Keycloak donde está configurado el cliente
- **Ejemplo**: `development`
- **Nota**: El realm debe existir en Keycloak

### Client ID
- **Descripción**: El identificador del cliente OAuth2 configurado en Keycloak
- **Ejemplo**: `gedsys-firmas`
- **Nota**: El Client ID debe estar configurado en Keycloak

### Redirect URI
- **Descripción**: La URI a la que Keycloak redirigirá después de la autenticación
- **Ejemplo**: `firmasapp://callback`
- **Nota**: El Redirect URI debe estar registrado en Keycloak como Valid Redirect URI

## Notas de configuración

La interfaz incluye una sección de notas que te ayudará a configurar correctamente cada campo:

- La URL debe incluir el protocolo (https://)
- El realm debe existir en Keycloak
- El Client ID debe estar configurado en Keycloak
- El Redirect URI debe estar registrado en Keycloak
- Para development: https://keycloak.gedsys.co
- Para producción: verificar con el equipo de infraestructura

## Botones disponibles

### 🔄 Restablecer Valores
- Vuelve a cargar los valores originales de la configuración
- Útil si has hecho cambios y te has equivocado

### 💾 Guardar Configuración
- Valida y guarda la nueva configuración
- La configuración se aplicará en el próximo inicio de sesión
- Muestra un mensaje de confirmación cuando se guarda exitosamente

### ❌ Cancelar
- Cierra el diálogo sin guardar cambios
- La configuración original se mantiene sin cambios

## Flujo de trabajo

1. **Abrir el diálogo de configuración**: Haz clic en "⚙️ Configurar"
2. **Revisar la configuración actual**: La información actual se muestra en un recuadro verde
3. **Modificar los campos necesarios**: Cambia los valores según tus necesidades
4. **Validar**: La aplicación valida automáticamente los campos cuando intentas guardar
5. **Guardar**: Haz clic en "💾 Guardar Configuración"
6. **Confirmar**: Verifica el mensaje de confirmación
7. **Iniciar sesión**: Usa la nueva configuración en el próximo login

## Consideraciones importantes

### Cambios en tiempo de ejecución
- La configuración se actualiza inmediatamente después de guardar
- Si hay una sesión activa, esta se cerrará automáticamente
- Deberás iniciar sesión nuevamente con la nueva configuración

### Validación de URL
- La URL debe usar el protocolo HTTPS (recomendado para producción)
- La URL debe estar en formato correcto (ej: https://keycloak.example.com)
- No se aceptan URLs sin protocolo o con protocolo inválido

### Seguridad
- La configuración se valida antes de guardarse
- Los campos obligatorios no pueden estar vacíos
- La aplicación garantiza que los cambios sean seguros y válidos

## Solución de problemas

### La URL no se guarda correctamente
- Verifica que la URL incluya el protocolo (https://)
- Asegúrate de que la URL esté en formato correcto
- Revisa que no haya espacios en blanco al inicio o final

### No puedo iniciar sesión después de cambiar la configuración
- Verifica que la URL de Keycloak sea correcta
- Asegúrate de que el realm exista en Keycloak
- Confirma que el Client ID esté configurado correctamente
- Verifica que el Redirect URI esté registrado en Keycloak

### Error de validación
- Revisa que todos los campos obligatorios estén completos
- Verifica el formato de la URL
- Asegúrate de que no haya caracteres inválidos

## Configuración por defecto

La configuración por defecto está definida en el archivo `appsettings.json`:

```json
{
  "Keycloak": {
    "Url": "https://keycloak.gedsys.co",
    "Realm": "development",
    "ClientId": "gedsys-firmas",
    "RedirectUri": "firmasapp://callback",
    "AuthorizationEndpoint": "/protocol/openid-connect/auth",
    "TokenEndpoint": "/protocol/openid-connect/token",
    "Scope": "openid profile email"
  }
}
```

## Implementación técnica

### Archivos modificados

1. **Views/KeycloakSettingsDialog.xaml**: Interfaz de usuario del diálogo de configuración
2. **Views/KeycloakSettingsDialog.xaml.cs**: Lógica del diálogo de configuración
3. **Services/KeycloakAuthService.cs**: Método UpdateSettings para actualizar la configuración
4. **Models/MainViewModel.cs**: Método UpdateKeycloakSettings para propagar los cambios
5. **MainWindow.xaml**: Botón de configuración en la interfaz principal
6. **MainWindow.xaml.cs**: Handler del botón de configuración
7. **App.xaml.cs**: Inyección de dependencias actualizada
8. **Views/LoginView.xaml**: Botón de configuración en la ventana de login
9. **Views/LoginView.xaml.cs**: Handler del botón de configuración en login

### Principios aplicados

- **Claridad**: Código claro y expresivo con nombres descriptivos
- **Simplicidad**: Mínima complejidad manteniendo solo lo necesario
- **Sin duplicidades**: Eliminación de código repetido
- **Validación robusta**: Tests fiables y mantenibles

## Soporte

Si tienes problemas con la configuración de Keycloak, contacta al equipo de infraestructura de Gedsys para obtener la información correcta de tu entorno.
