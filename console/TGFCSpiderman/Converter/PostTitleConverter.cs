using System;
using System.Windows;
using System.Windows.Data;

namespace taiyuanhitech.TGFCSpiderman.Converter
{
    class PostTitleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue) return "";
            var threadTitle = (string)values[0];
            var order = (int)values[1];
            return order == 1 ? threadTitle : "RE:" + threadTitle;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
