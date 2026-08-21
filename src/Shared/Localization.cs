using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Win32;

namespace SolidTweaks.Shared
{
    internal enum UiLanguage
    {
        English,
        Portuguese
    }

    internal static class UiLanguageSelector
    {
        private const string RegistryPath = @"Software\SolidTweaks\Preferences";
        private const string RegistryValueName = "UiLanguage";
        private static UiLanguage current = ReadStoredLanguage() ?? UiLanguage.English;

        public static UiLanguage Current { get { return current; } }

        public static void Initialize(string solidWorksLanguage)
        {
            UiLanguage? stored = ReadStoredLanguage();
            if (stored.HasValue)
            {
                current = stored.Value;
                return;
            }

            current = Select(solidWorksLanguage);
            Save(current);
        }

        public static void SetCurrent(UiLanguage language)
        {
            Save(language);
            current = language;
        }

        internal static UiLanguage Select(string cultureName)
        {
            string normalized = (cultureName ?? string.Empty).Trim();
            return string.Equals(normalized, "pt-BR", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "pt-PT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "portugese-brazilian", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "portuguese-brazilian", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "1046", StringComparison.OrdinalIgnoreCase)
                ? UiLanguage.Portuguese
                : UiLanguage.English;
        }

        private static UiLanguage? ReadStoredLanguage()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    string value = key == null ? null : key.GetValue(RegistryValueName) as string;
                    if (string.Equals(value, "pt-BR", StringComparison.OrdinalIgnoreCase))
                    {
                        return UiLanguage.Portuguese;
                    }
                    if (string.Equals(value, "en", StringComparison.OrdinalIgnoreCase))
                    {
                        return UiLanguage.English;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static void Save(UiLanguage language)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Could not persist the user interface language.");
                }
                key.SetValue(
                    RegistryValueName,
                    language == UiLanguage.Portuguese ? "pt-BR" : "en",
                    RegistryValueKind.String);
            }
        }
    }

    internal static class UiText
    {
        private static readonly IDictionary<string, string> Portuguese =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Help", "Ajuda" },
                { "Quick tweaks for SOLIDWORKS", "Ajustes rápidos para SOLIDWORKS" },
                { "Open the CG3D channel on YouTube", "Abrir o canal CG3D no YouTube" },
                { "CG3D channel logo", "Logo do canal CG3D" },
                { "Follow on YouTube", "Siga no Youtube" },
                { "CG3D channel", "Canal CG3D" },
                { "Open the SolidTweaks project on GitHub", "Abrir o projeto SolidTweaks no GitHub" },
                { "ENVIRONMENT", "AMBIENTE" },
                { "LANGUAGE", "IDIOMA" },
                { "Change the Solid Tweaks language", "Alterar o idioma do Solid Tweaks" },
                { "Getting version...", "Obtendo versão..." },
                { "Getting graphics card...", "Obtendo placa de vídeo..." },
                { "Getting .NET Framework...", "Obtendo .NET Framework..." },
                { "GRAPHICS CARD", "PLACA DE VÍDEO" },
                { ".NET INSTALLED", ".NET INSTALADO" },
                { "Reconnect", "Reconectar" },
                { "Prevents the camera from automatically rotating normal to the plane when creating or editing a sketch.", "Impede que a câmera gire automaticamente para a normal do plano ao criar ou editar um esboço." },
                { "When enabled, Space runs Normal To for the selection.", "Quando ativo, Espaço executa Normal To para a seleção." },
                { "When enabled, F runs Make Fixed on the selected sketch entity.", "Quando ativo, F executa Tornar fixo sobre a entidade de esboço selecionada." },
                { "When enabled, C toggles selected sketch entities between regular and construction geometry.", "Quando ativo, C alterna as entidades de esboço selecionadas entre geometria normal e linha de construção." },
                { "When enabled, M opens Measure. When disabled, M is removed from Measure.", "Quando ativo, M abre Medir. Quando desativado, M é removida de Medir." },
                { "Rotate when creating a sketch", "Girar ao criar esboço" },
                { "Space key (3D View -> Normal To)", "Tecla Espaço (3D view -> Normal)" },
                { "F key (Make Fixed)", "Tecla F (Tornar fixo)" },
                { "C key (Construction Geometry)", "Tecla C (Linha de construção)" },
                { "M key (Measure)", "Tecla M (Medição)" },
                { "(Make Fixed)", "(Tornar fixo)" },
                { "(Construction line)", "(Linha de construção)" },
                { "(Measure)", "(Medição)" },
                { "Construction line", "Linha de construção" },
                { "Measurement", "Medição" },
                { "Set key", "Definir tecla" },
                { "Set shortcut", "Definir atalho" },
                { "Press the desired key for the shortcut...", "Pressione a tecla desejada para o atalho..." },
                { "Cancel", "Cancelar" },
                { "Clear key", "Limpar tecla" },
                { "Current key: {0}", "Tecla atual: {0}" },
                { "Use one key without Ctrl, Alt, or Shift.", "Use uma tecla sem Ctrl, Alt ou Shift." },
                { "Choose a keyboard shortcut to run Make Fixed on the selected sketch entity.", "Escolha uma tecla para executar Tornar fixo sobre a entidade de esboço selecionada." },
                { "Choose a keyboard shortcut to toggle construction geometry.", "Escolha uma tecla para alternar a geometria de construção." },
                { "Choose a keyboard shortcut to open Measure.", "Escolha uma tecla para abrir Medir." },
                { "Choose a keyboard shortcut to open Offset Entities.", "Escolha uma tecla para abrir Offset de entidades." },
                { "Help: {0}", "Ajuda: {0}" },
                { "Requirement for enabling the feature", "Requisito para habilitar o recurso" },
                { "Exports the part or assembly to STEP AP214 in the same folder, adding the time to the file name. Only visible bodies and components are included.", "Exporta a peça ou montagem para STEP AP214 na mesma pasta, com o horário no nome. Inclui somente corpos e componentes visíveis." },
                { "Adds a command to the Solid Tweaks tab for exporting sheet-metal flat patterns from parts and assemblies.", "Adiciona à guia Solid Tweaks o comando para exportar planificações de chapa metálica em peças e montagens." },
                { "FEATURES", "RECURSOS" },
                { "Export STEP", "Exportar STEP" },
                { "Export Sheet Metal DXF", "Exportar Chapas DXF" },
                { "VISUAL", "VISUAL" },
                { "Enhanced Graphics Performance", "Desempenho gráfico aprimorado" },
                { "GPU silhouette edges", "Arestas de silhueta por GPU" },
                { "Full antialiasing", "Antialiasing completo" },
                { "Enables RealView for the graphics renderer used by SOLIDWORKS. NVIDIA, AMD, and Intel cards are supported.", "Habilita o RealView para o renderer gráfico usado pelo SOLIDWORKS. Placas NVIDIA, AMD e Intel são compatíveis." },
                { "Enables the enhanced graphics pipeline in SOLIDWORKS.", "Habilita o pipeline de gráficos aprimorados do SOLIDWORKS." },
                { "Uses GPU acceleration for silhouette edges. Enhanced Graphics Performance must also be enabled.", "Usa aceleração da GPU nas arestas de silhueta. O Desempenho gráfico aprimorado também deve estar habilitado." },
                { "Applies full-scene antialiasing instead of antialiasing only shaded edges and sketches.", "Aplica antialiasing à cena completa em vez de somente às arestas sombreadas e aos esboços." },
                { "The graphics renderer could not be identified.", "Não foi possível identificar o renderer gráfico." },
                { "The graphics renderer name is not valid for enabling RealView.", "O nome do renderer gráfico não é válido para habilitar o RealView." },
                { "RealView supports NVIDIA, AMD, and Intel graphics cards.", "O RealView é compatível com placas NVIDIA, AMD e Intel." },
                { "Enabling {0}...", "Habilitando {0}..." },
                { "Disabling {0}...", "Desabilitando {0}..." },
                { "Enable Enhanced Graphics Performance before enabling GPU silhouette edges.", "Habilite o Desempenho gráfico aprimorado antes das arestas de silhueta por GPU." },
                { "Requires Enhanced Graphics Performance", "Requer Desempenho gráfico aprimorado" },
                { "Could not apply the visual change: {0}", "Não foi possível aplicar o ajuste visual: {0}" },
                { "{0} enabled. Restart SOLIDWORKS if it is open.", "{0} habilitado. Reinicie o SOLIDWORKS caso esteja aberto." },
                { "{0} disabled. Restart SOLIDWORKS if it is open.", "{0} desabilitado. Reinicie o SOLIDWORKS caso esteja aberto." },
                { "Update", "Atualizar" },
                { "Initializing...", "Inicializando..." },
                { "Open the log folder", "Abrir a pasta de logs" },
                { "Log unavailable", "Log indisponível" },
                { "Reading system information...", "Lendo informações do sistema..." },
                { "Looking for an active SOLIDWORKS session...", "Procurando uma sessão ativa do SOLIDWORKS..." },
                { "Connection failed: {0}", "Falha ao conectar: {0}" },
                { "An add-in update is available. Click Update.", "Uma atualização do add-in está disponível. Clique em Atualizar." },
                { "SOLIDWORKS is not open. Open it and click Reconnect.", "SOLIDWORKS não está aberto. Abra-o e clique em Reconectar." },
                { "An add-in update is available. Save your projects and click Update.", "Uma atualização do add-in está disponível. Salve seus projetos e clique em Atualizar." },
                { "Connected. Features synchronized.", "Conectado. Recursos sincronizados." },
                { "Connected. Select a feature to install or repair the Solid Tweaks tab.", "Conectado. Marque um recurso para instalar ou reparar a guia Solid Tweaks." },
                { "Could not read the preference: {0}", "Falha ao ler a preferência: {0}" },
                { "Enabling automatic rotation...", "Ativando giro automático..." },
                { "Disabling automatic rotation...", "Desativando giro automático..." },
                { "Automatic rotation enabled.", "Giro automático ativado." },
                { "Automatic rotation disabled.", "Giro automático desativado." },
                { "Could not apply the change: {0}", "Não foi possível aplicar: {0}" },
                { "The requested change could not be confirmed.", "Não foi possível confirmar a alteração solicitada." },
                { "The saved state does not match the requested change.", "O estado salvo não corresponde à alteração solicitada." },
                { "Permanent shortcut saved: Space runs Normal To.", "Atalho permanente salvo: Espaço executa Normal To." },
                { "Permanent shortcut saved: Space opens 3D View.", "Atalho permanente salvo: Espaço abre 3D View." },
                { "Permanent shortcut saved: F runs Make Fixed on the selection.", "Atalho permanente salvo: F executa Tornar fixo sobre a seleção." },
                { "Permanent shortcut restored: F runs Zoom to Fit.", "Atalho permanente restaurado: F executa Zoom para ajustar." },
                { "Permanent shortcut saved: C toggles construction geometry.", "Atalho permanente salvo: C alterna geometria de construção." },
                { "Permanent shortcut restored: C runs Circle.", "Atalho permanente restaurado: C executa Círculo." },
                { "Permanent shortcut saved: M runs Measure.", "Atalho permanente salvo: M executa Medir." },
                { "Permanent shortcut removed: M no longer runs Measure.", "Atalho permanente removido: M não executa mais Medir." },
                { "Could not save the shortcut: {0}", "Não foi possível salvar o atalho: {0}" },
                { "Removing the previous fixed shortcuts...", "Removendo os atalhos fixos anteriores..." },
                { "Removing the shortcut...", "Removendo o atalho..." },
                { "Saving the {0} shortcut...", "Salvando o atalho {0}..." },
                { "Shortcut removed from {0}.", "Atalho removido de {0}." },
                { "Shortcut saved: {0} runs {1}.", "Atalho salvo: {0} executa {1}." },
                { "Shortcut change canceled.", "Alteração do atalho cancelada." },
                { "The {0} shortcut is assigned to {1}. Replace it?", "O atalho {0} está atribuído a {1}. Deseja substituí-lo?" },
                { "The {0} key is already assigned. Replace the existing shortcut?\n\n{1}", "A tecla {0} já está atribuída. Deseja substituir o atalho existente?\n\n{1}" },
                { "SOLIDWORKS must be open", "Necessário abrir o SOLIDWORKS" },
                { "An open document is required", "Necessário um documento aberto" },
                { "Save all open projects before continuing.\n\nSOLIDWORKS will close to update the Solid Tweaks tab and reopen when the update is complete.", "Salve todos os projetos abertos antes de continuar.\n\nO SOLIDWORKS será fechado para atualizar a guia Solid Tweaks e será aberto novamente ao concluir." },
                { "Update Solid Tweaks", "Atualizar Solid Tweaks" },
                { "Closing SOLIDWORKS for the update...", "Fechando o SOLIDWORKS para atualizar..." },
                { "Installing the new add-in version...", "Instalando a nova versão do add-in..." },
                { "Update installed. Reopening SOLIDWORKS...", "Atualização instalada. Reabrindo o SOLIDWORKS..." },
                { "Solid Tweaks was updated successfully. Previous features were restored.", "Solid Tweaks atualizado com sucesso. Recursos anteriores restaurados." },
                { "The update was installed, but the tab could not yet be confirmed in SOLIDWORKS.", "A atualização foi instalada, mas a guia ainda não pôde ser confirmada no SOLIDWORKS." },
                { "Could not update: {0}", "Não foi possível atualizar: {0}" },
                { "SOLIDWORKS is running but did not accept the connection required to close it safely.", "O SOLIDWORKS está em execução, mas não aceitou a conexão necessária para fechá-lo com segurança." },
                { "SOLIDWORKS did not close within the expected time. The update was canceled.", "O SOLIDWORKS não foi encerrado dentro do tempo esperado. A atualização foi cancelada." },
                { "Adding {0} to the Solid Tweaks tab...", "Adicionando {0} na guia Solid Tweaks..." },
                { "Removing {0} from the Solid Tweaks tab...", "Removendo {0} na guia Solid Tweaks..." },
                { "{0} was added to the Solid Tweaks tab.", "{0} adicionado à guia Solid Tweaks." },
                { "{0} was removed; the tab still contains the other features.", "{0} removido; a guia permanece com os demais recursos." },
                { "The last feature was removed; the Solid Tweaks tab was deleted.", "Último recurso removido; a guia Solid Tweaks foi excluída." },
                { "Could not change the tab: {0}", "Não foi possível alterar a guia: {0}" },
                { "The state of {0} could not be confirmed.", "O estado do recurso {0} não pôde ser confirmado." },
                { "No Solid Tweaks tab feature is configured for the update.", "Nenhum recurso da guia Solid Tweaks está configurado para a atualização." },
                { "The add-in update could not be confirmed.", "A atualização do add-in não pôde ser confirmada." },
                { "The Solid Tweaks tab could not be confirmed.", "A guia Solid Tweaks não pôde ser confirmada." },
                { "The removal of the Solid Tweaks tab could not be confirmed.", "A remoção da guia Solid Tweaks não pôde ser confirmada." },
                { "Windows authorization is required to change the SOLIDWORKS add-in.", "A autorização do Windows é necessária para alterar o add-in do SOLIDWORKS." },
                { "Log: {0}", "Log: {0}" },
                { "Open log folder: {0}", "Abrir pasta de logs: {0}" },
                { "Could not open the log folder.\n\n{0}\n\nPath: {1}", "Não foi possível abrir a pasta de logs.\n\n{0}\n\nCaminho: {1}" },
                { "Could not open the CG3D channel.\n\n{0}\n\n{1}", "Não foi possível abrir o canal CG3D.\n\n{0}\n\n{1}" },
                { "Could not open the SolidTweaks repository.\n\n{0}\n\n{1}", "Não foi possível abrir o repositório do SolidTweaks.\n\n{0}\n\n{1}" },
                { "The log folder is not available yet.", "A pasta do log ainda não está disponível." },
                { "Not identified", "Não identificada" },
                { ".NET Framework 4.x not identified", ".NET Framework 4.x não identificado" },
                { "Not connected", "Não conectado" },
                { "SOLIDWORKS 2025 not found", "SOLIDWORKS 2025 não localizado" },
                { "The SOLIDWORKS executable was not found.", "O executável do SOLIDWORKS não foi encontrado." },
                { "The document template was not found.", "O template do documento não foi encontrado." },
                { "Could not create the {0} document.", "Não foi possível criar o documento {0}." },
                { "SOLIDWORKS could not load the Solid Tweaks tab.", "O SOLIDWORKS não conseguiu carregar a guia Solid Tweaks." },
                { "The add-in was loaded, but the Solid Tweaks tab is incomplete.", "O add-in foi carregado, mas a guia Solid Tweaks está incompleta." },
                { "The Solid Tweaks add-in could not be unloaded. Code: {0}.", "O add-in Solid Tweaks não pôde ser descarregado. Código: {0}." },
                { "No active SOLIDWORKS session was found.", "Nenhuma sessão ativa do SOLIDWORKS foi encontrada." },
                { "No active document was found.", "Nenhum documento ativo foi encontrado." },
                { "Opening SOLIDWORKS keyboard customization...", "Abrindo a personalização de teclado do SOLIDWORKS..." },
                { "Locating the {0} command...", "Localizando o comando {0}..." },
                { "Assigning the {0} key and resolving conflicts...", "Atribuindo a tecla {0} e resolvendo conflitos..." },
                { "Removing the {0} key...", "Removendo a tecla {0}..." },
                { "Confirming and saving the native shortcut...", "Confirmando e salvando o atalho nativo..." },
                { "Space", "Espaço" },
                { "Normal To", "Normal a" },
                { "Make Fixed", "Tornar fixo" },
                { "Zoom to Fit", "Zoom para ajustar" },
                { "Construction Geometry", "Geometria de construção" },
                { "Circle", "Círculo" },
                { "Measure", "Medir" },
                { "Offset Entities", "Offset de entidades" },
                { "+ Options", "+ Opções" },
                { "- Options", "- Opções" },
                { "Show shortcut options", "Mostrar opções de atalhos" },
                { "Hide shortcut options", "Ocultar opções de atalhos" },
                { "Show visual options", "Mostrar opções visuais" },
                { "Hide visual options", "Ocultar opções visuais" },
                { "View Orientation", "Orientação" },
                { "Choose what to do after generating the STEP file:", "Escolha o que fazer após gerar o arquivo STEP:" },
                { "Export", "Exportar" },
                { "Send to Print", "Enviar para Impressão" },
                { "Open folder", "Abrir pasta" },
                { "Could not save the Open folder option.\n\n{0}", "Não foi possível salvar a opção Abrir pasta.\n\n{0}" },
                { "Choose which sheet-metal bodies to export:", "Escolha quais corpos de chapa metálica deseja exportar:" },
                { "No body is selected. Export all sheet-metal bodies:", "Nenhum corpo está selecionado. Exporte todas as chapas:" },
                { "Selected only", "Apenas selecionados" },
                { "All sheet-metal bodies", "Todas as chapas" },
                { "Bend lines", "Linhas de dobra" },
                { "Could not save the Bend lines option.\n\n{0}", "Não foi possível salvar a opção Linhas de dobra.\n\n{0}" },
                { "Export the active document to STEP", "Exportar o documento ativo para STEP" },
                { "Export sheet-metal flat patterns to DXF", "Exportar planificações de chapa metálica para DXF" },
                { "Solid Tweaks features", "Recursos do Solid Tweaks" },
                { "Solid Tweaks features in CommandManager", "Recursos do Solid Tweaks no CommandManager" },
                { "Open a part or assembly before exporting.", "Abra uma peça ou montagem antes de exportar." },
                { "STEP export is available only for parts and assemblies.", "A exportação STEP está disponível somente para peças e montagens." },
                { "Save the part or assembly before exporting the STEP file.", "Salve a peça ou montagem antes de exportar o arquivo STEP." },
                { "Could not export the STEP file.\n\n{0}", "Não foi possível exportar o STEP.\n\n{0}" },
                { "The STEP file was exported, but the folder could not be opened.\n\n{0}", "O STEP foi exportado, mas a pasta não pôde ser aberta.\n\n{0}" },
                { "The STEP file was exported, but it could not be opened in the default application.\n\n{0}", "O STEP foi exportado, mas não pôde ser aberto no aplicativo padrão.\n\n{0}" },
                { "SOLIDWORKS did not complete the export. Error code: {0}; warnings: {1}.", "O SOLIDWORKS não concluiu a exportação. Código de erro: {0}; avisos: {1}." },
                { "STEP exported ({0} item): {1}", "STEP exportado ({0} item): {1}" },
                { "STEP exported ({0} items): {1}", "STEP exportado ({0} itens): {1}" },
                { "No visible body or component was found for export.", "Nenhum corpo ou componente visível foi encontrado para exportar." },
                { "Not all visible geometry could be selected for export.", "Nem toda a geometria visível pôde ser selecionada para exportação." },
                { "A STEP preference could not be applied.", "Uma preferência STEP não pôde ser aplicada." },
                { "No sheet-metal body was found.", "Nenhum corpo de chapa metálica foi encontrado." },
                { "No sheet-metal body was found for export.", "Nenhum corpo de chapa metálica foi encontrado para exportar." },
                { "The selection contains no parts or sheet-metal bodies that can be exported.", "A seleção não contém peças ou corpos de chapa metálica que possam ser exportados." },
                { "No valid part was found in the open document.", "Nenhuma peça válida foi encontrada no documento aberto." },
                { "Could not export the DXF files.\n\n{0}", "Não foi possível exportar os DXFs.\n\n{0}" },
                { "Exporting {0}...", "Exportando {0}..." },
                { "{0} DXF file exported successfully.", "{0} DXF exportado com sucesso." },
                { "{0} DXF files exported successfully.", "{0} DXFs exportados com sucesso." },
                { "{0} DXF file exported with warnings.", "{0} DXF exportado com avisos." },
                { "{0} DXF files exported with warnings.", "{0} DXFs exportados com avisos." },
                { "Save part {0} before exporting the DXF file.", "Salve a peça {0} antes de exportar o DXF." },
                { "Could not activate configuration {0} in {1}.", "Não foi possível ativar a configuração {0} em {1}." },
                { "Could not activate part {0}.", "Não foi possível ativar a peça {0}." },
                { "The flat pattern for body {0} was not found in {1}.", "A planificação do corpo {0} não foi encontrada em {1}." },
                { "unnamed", "sem nome" },
                { "The thickness of body {0} was not found in {1}.", "A espessura do corpo {0} não foi encontrada em {1}." },
                { "Could not select the flat pattern for {0}.", "Não foi possível selecionar a planificação de {0}." },
                { "SOLIDWORKS could not export {0}.", "O SOLIDWORKS não conseguiu exportar {0}." },
                { "Part {0} could not be loaded by the assembly.", "A peça {0} não pôde ser carregada pela montagem." },
                { "Open a part or assembly before exporting DXF files.", "Abra uma peça ou montagem antes de exportar DXF." },
                { "DXF export is available only for parts and assemblies.", "A exportação DXF está disponível somente para peças e montagens." },
                { "Unexpected error. See the log for details.", "Erro inesperado. Consulte o log para obter detalhes." }
            };

        public static bool IsPortuguese { get { return UiLanguageSelector.Current == UiLanguage.Portuguese; } }

        public static string Get(string english)
        {
            return GetForLanguage(UiLanguageSelector.Current, english);
        }

        internal static string GetForLanguage(UiLanguage language, string english)
        {
            string value;
            return language == UiLanguage.Portuguese && Portuguese.TryGetValue(english, out value)
                ? value
                : english;
        }

        public static string Format(string english, params object[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(english), arguments);
        }

        public static string UserMessage(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                var localized = current as UserFacingException;
                if (localized != null)
                {
                    return localized.UserMessage;
                }
            }
            return Get("Unexpected error. See the log for details.");
        }
    }

    internal sealed class UserFacingException : Exception
    {
        public UserFacingException(string diagnosticMessage, string userMessage)
            : base(diagnosticMessage)
        {
            UserMessage = userMessage;
        }

        public UserFacingException(string diagnosticMessage, string userMessage, Exception innerException)
            : base(diagnosticMessage, innerException)
        {
            UserMessage = userMessage;
        }

        public string UserMessage { get; private set; }
    }
}
