using System; using System.Collections.Generic;
using System.Linq;

namespace SeamlessPuzzle.Utils
{
    public class UndoManager<T>
    {
        private readonly Stack<T> _undoStack = new();
        private readonly Stack<T> _redoStack = new();
        private readonly int _maxHistory = 20;

        public void PushState(T state)
        {
            _undoStack.Push(state);
            _redoStack.Clear();

            if (_undoStack.Count > _maxHistory)
            {
                // 获取要弹出的项目
                var itemToRemove = _undoStack.Pop();
                // 尝试释放可释放对象
                DisposeItem(itemToRemove);
            }
        }

        public T? Undo()
        {
            if (_undoStack.Count == 0) return default;

            var current = _undoStack.Pop();
            _redoStack.Push(current);
            return _undoStack.Count > 0 ? _undoStack.Peek() : default;
        }

        public T? Redo()
        {
            if (_redoStack.Count == 0) return default;

            var next = _redoStack.Pop();
            _undoStack.Push(next);
            return next;
        }

        public bool CanUndo => _undoStack.Count > 1;
        public bool CanRedo => _redoStack.Count > 0;

        public void Clear()
        {
            // 释放undo堆栈中的所有可释放对象
            foreach (var item in _undoStack)
            {
                DisposeItem(item);
            }
            
            // 释放redo堆栈中的所有可释放对象
            foreach (var item in _redoStack)
            {
                DisposeItem(item);
            }
            
            // 清空堆栈
            _undoStack.Clear();
            _redoStack.Clear();
        }
        
        /// <summary>
        /// 尝试释放对象，如果它是可释放的或包含可释放对象的集合
        /// </summary>
        /// <param name="item">要释放的项目</param>
        private void DisposeItem(T item)
        {
            // 如果项目本身是可释放的，则释放它
            if (item is IDisposable disposableItem)
            {
                try
                {
                    disposableItem.Dispose();
                }
                catch { /* 忽略异常 */ }
                return;
            }
            
            // 处理常见的集合类型
            // 对于ImageModel列表的特殊处理
            if (item is IEnumerable<SeamlessPuzzle.Models.ImageModel> imageModels)
            {
                foreach (var imageModel in imageModels)
                {
                    try
                    {
                        imageModel.Dispose();
                    }
                    catch { /* 忽略异常 */ }
                }
                return;
            }
            
            // 通用集合处理
            if (item is System.Collections.IEnumerable collection)
            {
                foreach (var obj in collection)
                {
                    if (obj is IDisposable disposableObj)
                    {
                        try
                        {
                            disposableObj.Dispose();
                        }
                        catch { /* 忽略异常 */ }
                    }
                }
            }
        }
    }
}