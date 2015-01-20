using System;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace taiyuanhitech.TGFCSpiderman.Converter
{
    class ScrubHtmlConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var html = (string)value;
            var scrubed = Regex.Replace(html, @"<[^>]+>|&nbsp;", "").Trim();
            return Regex.Replace(scrubed, @"\s{2,}", " ");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
