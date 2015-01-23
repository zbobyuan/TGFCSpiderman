using System;
using System.Collections.Generic;
using System.Windows.Data;

namespace taiyuanhitech.TGFCSpiderman.Converter
{
    class SortOrderItemSourceConverter : IValueConverter
    {
        static readonly List<string> ListForTopicOnly;
        static readonly List<string> ListForAllPosts;
        static SortOrderItemSourceConverter()
        {
            ListForAllPosts = new List<string>{"发表时间","正分","负分","总分","争议度"};
            ListForTopicOnly = new List<string>(ListForAllPosts);
            ListForTopicOnly.Insert(1, "最后回复时间");
            ListForTopicOnly.Insert(2, "回复数");
        }
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? ListForTopicOnly : ListForAllPosts;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
