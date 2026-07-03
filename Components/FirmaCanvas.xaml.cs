using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Shapes;

namespace FirmasApp.Components;

public partial class FirmaCanvas : UserControl
{
    public FirmaCanvas()
    {
        InitializeComponent();

        // Configurar atributos predeterminados del InkCanvas
        if (FirmaInkCanvas != null)
        {
            var attributes = new DrawingAttributes
            {
                Color = Colors.Black,
                Width = 2.5,
                Height = 2.5
            };
            FirmaInkCanvas.DefaultDrawingAttributes = attributes;
        }
    }

    public void Limpiar()
    {
        if (FirmaInkCanvas != null)
        {
            FirmaInkCanvas.Strokes.Clear();
        }
    }

    public string? ObtenerFirmaDataUrl()
    {
        if (FirmaInkCanvas == null || FirmaInkCanvas.Strokes.Count == 0)
            return null;

        try
        {
            return ConvertirTrazosADataUrl();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al obtener firma: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    private string ConvertirTrazosADataUrl()
    {
        try
        {
            // Obtener el tamaño del canvas
            double width = FirmaInkCanvas.ActualWidth;
            double height = FirmaInkCanvas.ActualHeight;

            // Si el canvas no tiene tamaño, usar un tamaño predeterminado
            if (width <= 0 || height <= 0)
            {
                width = 400;
                height = 200;
            }

            // Crear un RenderTargetBitmap para renderizar los trazos
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Dibujar un fondo transparente
                context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

                // Crear un pen para dibujar los trazos
                var pen = new Pen(Brushes.Black, 2.5)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };

                // Dibujar cada trazo
                foreach (var stroke in FirmaInkCanvas.Strokes)
                {
                    var geometry = new StreamGeometry();
                    using (var geometryContext = geometry.Open())
                    {
                        bool first = true;
                        foreach (var point in stroke.StylusPoints)
                        {
                            var p = new Point(point.X, point.Y);
                            if (first)
                            {
                                geometryContext.BeginFigure(p, false, false);
                                first = false;
                            }
                            else
                            {
                                geometryContext.LineTo(p, true, true);
                            }
                        }
                        geometryContext.Close();
                    }
                    context.DrawGeometry(null, pen, geometry);
                }
            }

            // Renderizar a bitmap
            var bitmap = new RenderTargetBitmap(
                (int)width, (int)height,
                96, 96, // DPI
                PixelFormats.Pbgra32
            );
            bitmap.Render(visual);
            bitmap.Freeze();

            // Convertir a PNG
            return BitmapSourceToPngDataUrl(bitmap);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al convertir trazos a imagen: {ex.Message}", ex);
        }
    }

    private string BitmapSourceToPngDataUrl(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        var bytes = stream.ToArray();
        var base64 = Convert.ToBase64String(bytes);

        return $"data:image/png;base64,{base64}";
    }
}
