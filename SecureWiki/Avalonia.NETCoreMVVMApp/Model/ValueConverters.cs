using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Brushes = System.Drawing.Brushes;

namespace SecureWiki.Model
{
    public class WidthConverterSingle : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            Console.WriteLine("attempting to cast value to double");
            var tmp = (double) value;
            Console.WriteLine("tmp = " + tmp);

            if (tmp > 100)
            {
                tmp -= 58;
            }

            return tmp;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class TreeViewItemColourConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Console.WriteLine("Entering TreeViewItemColourConverter");
            bool newestRevisionSelected = (bool) value;

            // Console.WriteLine("bool is '{0}'", newestRevisionSelected);

            var output = new SolidColorBrush();
            if (newestRevisionSelected)
            {
                output = new SolidColorBrush(Colors.Transparent);
            }
            else
            {
                output = new SolidColorBrush(Colors.Chocolate);
                output.Opacity = 0.2;
            }

            return output;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FillerTextBlockWidthConverterMulti : IMultiValueConverter
    {
        // Calculate the width of the 'filler' textblock in the datatemplate for DataFileEntry
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {

            // Console.WriteLine("Entered multiconverter");
            // foreach (var item in values)
            // {
            //     Console.WriteLine("item='{0}'", item);
            // }
            
            var scrollViewerWidth = (double)values[0];
            var insideWidth = (double)values[1];
            var cb1 = (double)values[2];
            var cb2 = (double)values[3];
            var tb = (double)values[4];
            var tv = (double)values[5];

            double diff = 0;

            if (tv > scrollViewerWidth)
            {
                diff = tv - scrollViewerWidth;
            }

            var maxOutput = scrollViewerWidth - cb1 - cb2 - tb - diff;
            var output = insideWidth - cb1 - cb2 - tb - diff;

            if (output > maxOutput)
            {
                output = maxOutput;
            }

            if (output < 0)
            {
                output = 0;
            }

            // Console.WriteLine("returning output='{0}'", output);

            return output;
        }
    }
}