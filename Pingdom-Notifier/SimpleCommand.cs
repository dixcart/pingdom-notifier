using System;
using System.Windows;
using System.Windows.Input;

namespace PingdomNotifier
{
    class SimpleCommand : ICommand
    {
        public void Execute(object parameter)
        {
            System.Diagnostics.Process.Start("https://my.pingdom.com/checks/down");
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public event EventHandler CanExecuteChanged;
    }
}
