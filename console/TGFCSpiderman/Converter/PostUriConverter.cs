using System;
using System.Windows;
using System.Windows.Data;

namespace taiyuanhitech.TGFCSpiderman.Converter
{
    class PostUriConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue || values[2] == DependencyProperty.UnsetValue) return null;
            var postId = (long)values[0];
            var threadId = (int)values[1];
            var postOrder = (int)values[2];
            if (postOrder == 1)
            {
                return new Uri(string.Format("http://club.tgfcer.com/thread-{0}-1-1.html", threadId));
            }

            const int pageSize = 15;
            var pageIndex = (postOrder - 1) / pageSize + 1;
            return new Uri(string.Format("http://club.tgfcer.com/thread-{0}-{1}-1.html#pid{2}", threadId, pageIndex, postId));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
