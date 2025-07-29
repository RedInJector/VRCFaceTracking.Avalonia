using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRCFaceTracking.Avalonia.Helpers
{
    /// <summary>
    /// This is an extention for an ObservebleCollection class
    /// to do "batch" insertion. This class allows to insert multiple
    /// items raising only one event with "AddRange" method
    /// </summary>
    public class ObservableCollectionEx<T> : ObservableCollection<T>
    {
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            CheckReentrancy();

            var startIndex = Count;
            var addedItems = new List<T>();

            foreach (var item in items)
            {
                Items.Add(item);
                addedItems.Add(item);
            }

            if (addedItems.Count == 0)
                return;

            // Raise one notification for the entire batch
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                addedItems, startIndex));

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        }
    }
}
