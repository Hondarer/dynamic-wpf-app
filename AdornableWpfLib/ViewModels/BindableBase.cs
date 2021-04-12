using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AdornableWpfLib.ViewModels
{
    public class BindableBase : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        /// <summary>
        /// プロパティが変更されたときに発生するイベント ハンドラーを保持します。
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged = null;

        /// <summary>
        /// プロパティが既に目的の値と一致しているかどうかを確認します。
        /// 必要な場合のみ、プロパティを設定し、リスナーに通知します。
        /// </summary>
        /// <typeparam name="T">プロパティの型。</typeparam>
        /// <param name="storage">get アクセス操作子と set アクセス操作子両方を使用したプロパティへの参照。</param>
        /// <param name="value">プロパティに必要な値。</param>
        /// <param name="propertyName">
        /// リスナーに通知するために使用するプロパティの名前。
        /// この値は省略可能で、<see cref="CallerMemberNameAttribute"/> をサポートするコンパイラから呼び出す場合に自動的に指定できます。
        /// </param>
        /// <returns>
        /// 値が変更された場合は <c>true</c>、既存の値が目的の値に一致した場合は <c>false</c> を返します。
        /// </returns>
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] String propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// プロパティ値が変更されたことをリスナーに通知します。
        /// </summary>
        /// <param name="propertyName">リスナーに通知するために使用するプロパティの名前。
        /// この値は省略可能で、<see cref="CallerMemberNameAttribute"/> をサポートするコンパイラから呼び出す場合に自動的に指定できます。
        /// </param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler eventHandler = PropertyChanged;
            if (eventHandler != null)
            {
                eventHandler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

    }
}
