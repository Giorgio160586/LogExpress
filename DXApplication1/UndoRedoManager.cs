using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DXApplication1
{
    public class UndoRedoManager
    {
        private Stack<string> undoStack;
        private Stack<string> redoStack;
        public bool IsUndoOrRedo { get; private set; }

        public UndoRedoManager()
        {
            undoStack = new Stack<string>();
            redoStack = new Stack<string>();
        }

        public void AddState(string state)
        {
            if (undoStack.Count >= 1000)
            {
                var tempList = undoStack.ToList();
                tempList.RemoveAt(0);
                undoStack = new Stack<string>(tempList);
            }
            undoStack.Push(state);
            redoStack.Clear();
        }

        public void Undo(MemoEdit textBox)
        {
            if (undoStack.Count > 0)
            {
                IsUndoOrRedo = true;
                redoStack.Push(Convert.ToString(textBox.EditValue));
                textBox.EditValue = undoStack.Pop();
                IsUndoOrRedo = false;
            }
        }

        public void Redo(MemoEdit textBox)
        {
            if (redoStack.Count > 0)
            {
                IsUndoOrRedo = true;
                undoStack.Push(textBox.Text);
                textBox.EditValue = redoStack.Pop();
                IsUndoOrRedo = false;
            }
        }
    }
}
