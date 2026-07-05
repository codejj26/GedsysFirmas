# Guía de Logos para FirmasApp

## 📋 Estructura de Archivos

```
FirmasApp/
├── Assets/
│   └── logo.png                    # ← Coloca tu logo aquí
├── MainWindow.xaml                 # ← Logo en encabezado
├── Views/
│   ├── GestionFirmaView.xaml       # ← Logo en título
│   └── ...
└── FirmasApp.csproj               # ← Configuración de recursos
```

## 🎨 Implementación Completa

### 1. **Configuración del Proyecto (FirmasApp.csproj)**

```xml
<ItemGroup>
  <Resource Include="Assets\**\*.*" />
</ItemGroup>
```

Esto incluye todos los archivos de la carpeta `Assets/` como recursos de la aplicación.

### 2. **Uso en MainWindow.xaml**

#### **Logo en el encabezado:**
```xml
<StackPanel Orientation="Horizontal">
  <Image Source="/Assets/logo.png"
         Width="40"
         Height="40"
         Stretch="Uniform"/>
  <TextBlock Text="Gestión de Firmas Digitales"
             FontSize="24"
             VerticalAlignment="Center"/>
</StackPanel>
```

#### **Icono de la ventana:**
```xml
<Window Title="App de Firmas - gedsys2"
        Icon="/Assets/logo.png"
        ...>
```

### 3. **Uso en otras ventanas**

```xml
<Image Source="/Assets/logo.png"
       Width="32"
       Height="32"
       VerticalAlignment="Center"/>
```

## 📐 Especificaciones Técnicas

### **Formatos recomendados:**

| Formato | Ventajas | Uso recomendado |
|---------|----------|------------------|
| **PNG** | Transparencia, alta calidad | Encabezados, iconos |
| **JPG** | Tamaño reducido | Fotos, fondos |
| **ICO** | Múltiples tamaños | Iconos de ventana |

### **Tamaños recomendados:**

| Uso | Tamaño | Descripción |
|-----|--------|-------------|
| **Icono ventana** | 16x16, 32x32 | Barra de tareas |
| **Encabezado** | 40x40, 64x64 | Títulos de ventana |
| **Botones** | 24x24, 32x32 | Botones con iconos |
| **Alta calidad** | 256x256, 512x512 | Impresión, zoom |

### **Mejores prácticas:**

- ✅ **PNG con transparencia** para gráficos
- ✅ **Fondo cuadrado** para mejor escala
- ✅ **Alta resolución** (mínimo 256x256)
- ✅ **Nombre simple** (logo.png, icon.png)
- ✅ **Colores sobrios** que coincidan con la interfaz

## 🎯 Ejemplos de Uso

### **Logo con efectos visuales:**

```xml
<Image Source="/Assets/logo.png"
       Width="40"
       Height="40"
       Stretch="Uniform"
       RenderOptions.BitmapScalingMode="HighQuality">
  <Image.Effect>
    <DropShadowEffect Color="#CCCCCC"
                     BlurRadius="5"
                     ShadowDepth="2"
                     Opacity="0.3"/>
  </Image.Effect>
</Image>
```

### **Logo con tooltip:**

```xml
<Image Source="/Assets/logo.png"
       Width="32"
       Height="32"
       ToolTip="FirmasApp - Gestión de Firmas Digitales"/>
```

### **Logo como botón:**

```xml
<Button Width="40" Height="40" Padding="0">
  <Image Source="/Assets/logo.png"
         Width="24"
         Height="24"
         Stretch="Uniform"/>
</Button>
```

## 🔧 Troubleshooting

### **El logo no aparece:**

1. **Verificar que el archivo exista:**
   ```bash
   ls Assets/logo.png
   ```

2. **Verificar que esté en el .csproj:**
   ```xml
   <Resource Include="Assets\**\*.*" />
   ```

3. **Limpiar y recompilar:**
   ```bash
   dotnet clean
   dotnet build
   ```

### **El logo aparece distorsionado:**

Usa `Stretch="Uniform"` para mantener la proporción:
```xml
<Image Source="/Assets/logo.png"
       Stretch="Uniform"/>
```

### **El logo se ve borroso:**

Aumenta la calidad del renderizado:
```xml
<Image Source="/Assets/logo.png"
       RenderOptions.BitmapScalingMode="HighQuality"/>
```

## 🎨 Diseño de Logo Recomendado

### **Colores que combinan con la interfaz:**

- **Principal:** `#2196F3` (azul)
- **Secundario:** `#4CAF50` (verde)
- **Neutral:** `#555555` (gris oscuro)
- **Fondo:** `#FFFFFF` (blanco) o transparente

### **Estilo sobrio y profesional:**

- Formas geométricas simples
- Máximo 2-3 colores
- Tipografía legible
- Sin elementos decorativos excesivos

## 📦 Configuración Actual

La aplicación ya está configurada para usar logos en:

1. **MainWindow.xaml:**
   - Logo de 40x40 en el encabezado
   - Icono de ventana completo

2. **GestionFirmaView.xaml:**
   - Logo de 32x32 en el título

3. **FirmasApp.csproj:**
   - Todos los archivos de Assets/ como recursos

## 🚀 Próximos Pasos

1. **Coloca tu logo** en `Assets/logo.png`
2. **Compila** la aplicación
3. **Verifica** que aparezca en todas las ventanas
4. **Ajusta** tamaños si es necesario

## 💡 Consejo Extra

Si no tienes un logo, puedes:
1. Usar herramientas online como Canva, LogoMaker
2. Contratar a un diseñador en Fiverr, Upwork
3. Usar un generador de logos automático
4. Crear uno simple con texto y forma básica

---

**Nota:** Reemplaza el archivo `logo.png.placeholder` con tu logo real en formato PNG.
