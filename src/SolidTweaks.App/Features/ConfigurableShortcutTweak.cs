using System;
using SolidTweaks.Infrastructure.Persistence;
using SolidTweaks.Infrastructure.SolidWorks;
using SolidTweaks.Shared;

namespace SolidTweaks.Features
{
    internal sealed class ConfigurableShortcutTweak
    {
        private readonly ShortcutAction action;
        private readonly SolidWorksKeyboardShortcutService keyboardService;
        private readonly Action<string> reportProgress;

        public ConfigurableShortcutTweak(
            ShortcutAction action,
            SolidWorksKeyboardShortcutService keyboardService,
            Action<string> reportProgress)
        {
            this.action = action;
            this.keyboardService = keyboardService;
            this.reportProgress = reportProgress;
        }

        public ShortcutAction Action { get { return action; } }
        public ShortcutBinding Read() { return ShortcutBindingStore.Read(action); }

        public void Assign(ShortcutBinding requested)
        {
            if (requested == null)
            {
                throw new ArgumentNullException("requested");
            }

            ShortcutBinding previous = Read();
            ShortcutAction? conflict = ShortcutBindingStore.FindAction(requested, action);
            ShortcutBinding conflictingBinding = conflict.HasValue ? ShortcutBindingStore.Read(conflict.Value) : null;

            try
            {
                // O SOLIDWORKS mantém múltiplos atalhos no mesmo campo. Remover o
                // vínculo conhecido antes de digitar o novo evita que o editor apague
                // a tecla recém-adicionada ao processar Backspace.
                if (previous != null && !previous.SameInput(requested))
                {
                    keyboardService.RemoveForReplacement(action, previous, reportProgress);
                }

                keyboardService.Assign(action, requested, reportProgress);

                ShortcutBindingStore.Write(action, requested);
                if (conflict.HasValue)
                {
                    ShortcutBindingStore.Write(conflict.Value, null);
                }

            }
            catch
            {
                ShortcutBindingStore.Write(action, previous);
                if (conflict.HasValue)
                {
                    ShortcutBindingStore.Write(conflict.Value, conflictingBinding);
                }
                TryRestoreKeyboardState(requested, previous, conflict, conflictingBinding);
                throw;
            }
        }

        public void Clear()
        {
            ShortcutBinding previous = Read();
            if (previous == null)
            {
                return;
            }

            keyboardService.Remove(action, previous, reportProgress);
            ShortcutBindingStore.Write(action, null);
        }

        private void TryRestoreKeyboardState(
            ShortcutBinding requested,
            ShortcutBinding previous,
            ShortcutAction? conflict,
            ShortcutBinding conflictingBinding)
        {
            if (requested == null)
            {
                return;
            }
            try
            {
                keyboardService.Remove(action, requested, reportProgress);
                keyboardService.Restore(action, previous, reportProgress);
                if (conflict.HasValue)
                {
                    keyboardService.Restore(conflict.Value, conflictingBinding, reportProgress);
                }
            }
            catch
            {
                // Preserva a exceção original e registra a falha principal no chamador.
            }
        }
    }

    internal sealed class ShortcutBindingMigration
    {
        private const string LegacyFix = "keyboard.f-fix-selection";
        private const string LegacyConstruction = "keyboard.c-construction-geometry";
        private const string LegacyMeasure = "keyboard.m-measure";
        private readonly SolidWorksKeyboardShortcutService keyboardService;
        private readonly FeatureStateStore stateStore;
        private readonly Action<string> reportProgress;

        public ShortcutBindingMigration(
            SolidWorksKeyboardShortcutService keyboardService,
            FeatureStateStore stateStore,
            Action<string> reportProgress)
        {
            this.keyboardService = keyboardService;
            this.stateStore = stateStore;
            this.reportProgress = reportProgress;
        }

        public bool IsComplete { get { return ShortcutBindingStore.IsMigrationComplete(); } }

        public void Run()
        {
            if (IsComplete)
            {
                return;
            }

            bool fix = stateStore.Read(LegacyFix, false);
            bool construction = stateStore.Read(LegacyConstruction, false);
            bool measure = stateStore.Read(LegacyMeasure, false);
            keyboardService.RevertLegacyShortcuts(fix, construction, measure, reportProgress);

            ShortcutBindingStore.Write(ShortcutAction.MakeFixed, null);
            ShortcutBindingStore.Write(ShortcutAction.ConstructionGeometry, null);
            ShortcutBindingStore.Write(ShortcutAction.Measure, null);
            ShortcutBindingStore.Write(ShortcutAction.OffsetEntities, null);
            ShortcutBindingStore.MarkMigrationComplete();
        }
    }
}
