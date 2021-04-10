using System;
using System.Windows.Input;

namespace DynamicWpfApp.Commands
{
    public class DelegateCommand : ICommand
    {
        private Action<object> execute;

        private Func<object, bool> canExecute;

        public DelegateCommand(Action<object> execute) : this(execute, o => true)
        {
        }

        public DelegateCommand(Action<object> execute, Func<object, bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (canExecute == null)
            {
                return false;
            }

            return canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            if (execute == null)
            {
                return;
            }

            execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add
            {
                CommandManager.RequerySuggested += value;
            }
            remove
            {
                CommandManager.RequerySuggested -= value;
            }
        }

        public static void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
