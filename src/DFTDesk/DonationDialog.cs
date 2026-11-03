using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DFTDesk;

/// <summary>
/// Displays the owner's private payment QR codes. No save, copy, or download
/// action is exposed by the UI. QR assets are loaded from build-time embedded
/// resources or the user's local DFTDesk donation folder.
/// </summary>
internal sealed class DonationDialog : Window
{
    private static readonly string LocalDonationDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DFTDesk", "Donation");

    private readonly List<IDisposable> _ownedResources = [];

    public DonationDialog()
    {
        Title = "支持 DFTDesk";
        Width = 690;
        Height = 570;
        MinWidth = 690;
        MinHeight = 570;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;

        var root = new StackPanel
        {
            Margin = new Thickness(28),
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        root.Children.Add(new TextBlock
        {
            Text = "支持 DFTDesk",
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        root.Children.Add(new TextBlock
        {
            Text = "如果 DFTDesk 对你的科研工作有所帮助，欢迎支持项目持续开发。",
            FontSize = 13,
            Foreground = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var cards = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        cards.Children.Add(CreatePaymentCard("微信支付", "wechat.png", "wechat"));
        cards.Children.Add(CreatePaymentCard("支付宝", "alipay.png", "alipay"));
        root.Children.Add(cards);

        root.Children.Add(new TextBlock
        {
            Text = "感谢每一份支持，让科研工具持续进步。",
            FontSize = 12,
            Foreground = Brushes.DimGray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
        });

        var close = new Button
        {
            Content = "关闭",
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth = 104,
            Padding = new Thickness(22, 8),
        };
        close.Classes.Add("accent");
        close.Click += (_, _) => Close();
        root.Children.Add(close);

        Content = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(4),
            Child = root,
        };

        Closed += (_, _) =>
        {
            foreach (var resource in _ownedResources)
                resource.Dispose();
            _ownedResources.Clear();
        };
    }

    private Control CreatePaymentCard(string title, string fileName, string resourceKey)
    {
        var stack = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var qr = LoadQr(fileName, resourceKey);
        Control body;
        if (qr is not null)
        {
            body = new Border
            {
                Width = 270,
                Height = 270,
                Padding = new Thickness(12),
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                Child = new Image
                {
                    Source = qr,
                    Width = 244,
                    Height = 244,
                    Stretch = Stretch.Uniform,
                },
            };
        }
        else
        {
            body = new Border
            {
                Width = 270,
                Height = 270,
                Padding = new Thickness(18),
                Background = Brushes.WhiteSmoke,
                CornerRadius = new CornerRadius(14),
                Child = new TextBlock
                {
                    Text = $"尚未配置{title}二维码。\n\n请将 {fileName} 放入：\n{LocalDonationDirectory}",
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Brushes.DimGray,
                    FontSize = 12,
                },
            };
        }

        var frame = new Border
        {
            Child = body,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#E5E2EC")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(8),
        };
        stack.Children.Add(frame);
        return stack;
    }

    private Bitmap? LoadQr(string fileName, string resourceKey)
    {
        var resourceName = $"DFTDesk.Donation.{resourceKey}.png";
        var stream = typeof(DonationDialog).Assembly.GetManifestResourceStream(resourceName);
        if (stream is not null)
        {
            try
            {
                var bitmap = new Bitmap(stream);
                _ownedResources.Add(stream);
                _ownedResources.Add(bitmap);
                return bitmap;
            }
            catch
            {
                stream.Dispose();
            }
        }

        var candidates = new[]
        {
            Path.Combine(LocalDonationDirectory, fileName),
            Path.Combine(AppContext.BaseDirectory, "Donation", fileName),
        };
        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var localStream = File.OpenRead(path);
                var bitmap = new Bitmap(localStream);
                _ownedResources.Add(localStream);
                _ownedResources.Add(bitmap);
                return bitmap;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }
}
