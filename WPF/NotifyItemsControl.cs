using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows.Controls;

namespace ZExtensions.WPF
{
    public abstract class NotifyItemsControl : ItemsControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;


        protected void OnPropertyChanged<T>(Expression<Func<T>> expr)
        {
            string propertyName = LambdaHelper.GetParameterName(expr);
            this.OnPropertyChanged(propertyName);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}