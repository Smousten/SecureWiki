using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SecureWiki.Utilities
{
    public class TreeViewItemColourConverter : IValueConverter
    {
        // Return transparent or slightly opaque brush based input boolean value
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool newestRevisionSelected = (bool) value;

            SolidColorBrush output = newestRevisionSelected ? 
                new SolidColorBrush(Colors.Transparent) : 
                new SolidColorBrush(Colors.Chocolate) {Opacity = 0.2};

            return output;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FillerTextBlockWidthConverterMulti : IMultiValueConverter
    {
        // Calculate the width of the 'filler' TextBlock in the data template for DataFileEntry
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
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

            return output;
        }
    }
    
    public class AllStringsAreNotEmptyMultiConverter : IMultiValueConverter
    {
        // Check whether all input string are non-empty 
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            bool output = true;

            foreach (var item in values)
            {
                if (item == null || ((string) item).Length < 1)
                {
                    output = false;
                }
            }
            
            return output;
        }
    }
}