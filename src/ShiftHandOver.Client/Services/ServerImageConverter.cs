using System;
using System.Globalization;
using System.Windows.Data;

namespace ShiftHandOver.Client.Services
{
    public class ServerImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string? url = value as string;
            return ApiService.GetImageSource(url);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
