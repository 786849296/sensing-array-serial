﻿using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace uart
{
    //转换器，报错
    //internal class Icon_switch_converter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, string language)
    //    {
    //        if ((string)value == "\uE768")
    //            return true;
    //        else
    //            return false;
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, string language)
    //    {
    //        if ((bool)value)
    //            return "\uE768";
    //        else
    //            return "\uE769";
    //    }
    //}

    internal class ViewModel_switch : INotifyPropertyChanged 
    {
        private bool _isStartIcon;
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public ViewModel_switch() { isStartIcon = true; }
        public bool isStartIcon 
        { 
            get { return _isStartIcon; } 
            set { _isStartIcon = value;  OnPropertyChanged(); /*OnPropertyChanged("boolToGlyph");*/ } 
        }

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string boolToGlyph(bool isStart)
        {
            if (isStart)
                return "\uE768";
            else
                return "\uE769";
        }
    }

    internal class HeatMap_pixel : INotifyPropertyChanged
    {
        public int x;
        public int y;
        public ushort range = 32;
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private ushort _adcValue;
        public ushort adcValue
        {
            set
            {
                _adcValue = value;
                OnPropertyChanged();
            }
            get { return _adcValue; }
        }

        public HeatMap_pixel(int x, int y, ushort adcValue)
        {
            this.x = x;
            this.y = y;
            this.adcValue = adcValue;
        }

        public Color GetColor(ushort adcValue)
        {
            byte offset = (byte)(0xff - adcValue * 256 / range);
            return Color.FromArgb(0xff, 0xff, offset, offset);
        }

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
