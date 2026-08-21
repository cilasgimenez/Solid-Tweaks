# Solid Tweaks

Aplicativo WinForms leve para SOLIDWORKS 2025, desenvolvido em C# e .NET Framework 4.8.

## Regra de persistência

Todo recurso altera uma configuração persistente do SOLIDWORKS ou do add-in Solid Tweaks. Fechar o aplicativo não desfaz os ajustes. Teclas usam a personalização nativa; atalhos de mouse são escutados pelo add-in somente enquanto o SOLIDWORKS está em primeiro plano. Não há serviço ou processo auxiliar residente no Windows.

## Recursos

### Girar ao criar esboço

Reflete a opção nativa **Girar automaticamente a exibição na normal ao plano do esboço ao criar o esboço e editá-lo**. A alteração usa `SetUserPreferenceToggle`, é relida após a execução e permanece salva no SOLIDWORKS.

### Tecla Espaço (3D view -> Normal)

Personaliza o teclado nativo em **Ferramentas > Personalizar > Teclado**:

- marcado: associa Espaço permanentemente a **Normal To/Normal a** (`swCommands_NormalTo`, 169);
- desmarcado: associa Espaço permanentemente a **View Orientation/Orientação/3D View** (`swCommands_ViewOrientation`, 341);
- uma associação anterior de Espaço é substituída;
- a janela Personalizar pode aparecer brevemente enquanto a tabela é alterada e confirmada.

O registro `HKCU\Software\SolidTweaks\Features` guarda somente o último resultado confirmado para restaurar o checkbox rapidamente. O atalho funcional é armazenado pelo próprio SOLIDWORKS.

### Atalhos configuráveis

Os atalhos permanecem recolhidos por padrão no bloco **Tweaks**. O botão **+ Opções** expande o bloco para mostrar os quatro atalhos e **- Opções** volta ao modo compacto.

**Tornar fixo**, **Linha de construção**, **Medição** e **Offset de entidades** possuem um label clicável que mostra `Definir tecla` ou a entrada atribuída:

- o diálogo aceita uma única tecla sem modificadores, Mouse 3, Mouse 4 ou Mouse 5;
- clique esquerdo, clique direito, combinações e modificadores isolados são recusados;
- teclas são gravadas permanentemente em **Ferramentas > Personalizar > Teclado**;
- Mouse 3/4/5 são executados pelo add-in somente com o SOLIDWORKS em primeiro plano e um documento ativo;
- uma entrada pode pertencer a somente uma das quatro funções;
- conflitos são substituídos automaticamente e de forma silenciosa;
- **Limpar tecla** remove o vínculo atual, preservando outros atalhos do mesmo comando;
- o label é atualizado somente depois que a alteração é confirmada.

Na primeira execução desta versão, os antigos estados fixos F/C/M são desfeitos e os quatro atalhos configuráveis começam sem atribuição. A migração aguarda uma sessão conectada com documento ativo.

### Guia Solid Tweaks

Os checkboxes **Exportar STEP** e **Exportar Chapas DXF** controlam comandos independentes em uma única guia **Solid Tweaks** no CommandManager de peças e montagens.

- qualquer um dos recursos cria a guia sem depender do outro;
- cada checkbox adiciona ou remove somente seu próprio botão;
- quando o último recurso é removido, a guia e o add-in são removidos;
- quando os dois estão ativos, os dois botões permanecem na mesma guia;
- o add-in reconstrói somente as guias durante uma alteração, sem depender de unload/reload do componente .NET.

O componente é implantado de forma versionada em `%LOCALAPPDATA%\SolidTweaks\Addins`. Os checkboxes só permanecem marcados depois que o add-in confirma, nas guias de peça e montagem, exatamente os comandos configurados. O registro `HKCU\Software\SolidTweaks\Features` armazena quais comandos devem compor a guia.

Quando uma versão anterior do add-in está instalada, o aplicativo mostra o botão **Atualizar** no cartão **Recursos** e mantém os checkboxes conforme a configuração já persistida. Após a confirmação do usuário, o SOLIDWORKS é fechado, a nova DLL versionada é instalada e o SOLIDWORKS é aberto novamente. A atualização não força o encerramento do processo se ele não puder ser fechado com segurança e não altera quais comandos estavam habilitados.

### Exportar STEP

O botão **Exportar STEP**, com o ícone `Exportar_Step.png`, usa diretamente a API do SOLIDWORKS, sem macro externa, para:

O botão usa diretamente a API do SOLIDWORKS, sem macro externa, para:

- salvar em STEP AP214 com geometria sólida e de superfície;
- criar o arquivo na mesma pasta com o nome `<documento>_HHmmss.STEP`;
- exportar somente corpos visíveis de peças multicorpos;
- exportar somente componentes visíveis de montagens;
- avisar quando o documento ainda não foi salvo;
- oferecer **Exportar** ou **Enviar para Impressão** antes de gerar o arquivo;
- guardar a opção **Abrir pasta** e, quando marcada, abrir a pasta ao concluir a ação **Exportar**;
- ao enviar para impressão, abrir o STEP com o aplicativo padrão associado no Windows;
- restaurar as preferências STEP do usuário depois de cada exportação.

O Windows solicita autorização de administrador quando o add-in precisa ser instalado, atualizado ou removido, pois o SOLIDWORKS registra add-ins em `HKLM\SOFTWARE\SOLIDWORKS\Addins`. O Solid Tweaks pode ser fechado depois da configuração.

### Exportar Chapas DXF

O botão **Exportar Chapas DXF**, com o ícone `Exportar_DXF.png`, usa diretamente `IPartDoc.ExportToDWG2` para gerar a planificação dos corpos de chapa metálica, sem automatizar menus e sem deixar o modelo planificado.

- em uma peça, processa todos os corpos de chapa metálica;
- em uma montagem, percorre as peças referenciadas e processa cada documento uma vez;
- sempre abre um modal com **Apenas selecionados** e **Todas as chapas**; quando não existe seleção, a primeira opção fica indisponível;
- o modal contém **Linhas de dobra**, cujo estado é salvo no registro e restaurado na próxima execução;
- salva o DXF na pasta da peça com a espessura no início do nome, como `1,5mm - <arquivo>.dxf` para uma única chapa ou `1,5mm - <arquivo>(<corpo>).dxf` para peças multicorpos;
- o checkbox **Linhas de dobra** inclui ou omite as linhas de dobra do DXF;
- peças ainda não salvas são ignoradas com um aviso, sem interromper as demais exportações;
- a configuração ativa, o documento ativo e a seleção original são restaurados ao final.

O comando é executado dentro do SOLIDWORKS pela guia **Solid Tweaks**. O formulário principal apenas habilita ou desabilita sua presença na guia.

### Visual

O bloco **Visual** reúne quatro ajustes persistentes do registro do SOLIDWORKS 2025. O estado dos checkboxes é lido diretamente das configurações efetivas, e não de uma cópia mantida pelo Solid Tweaks.

Por padrão, somente **RealView** permanece visível. **+ Opções** expande os outros três ajustes usando a rolagem vertical, sem redimensionar o Form, e **- Opções** recolhe o bloco novamente.

- **RealView** identifica primeiro o renderer que o próprio SOLIDWORKS registrou e usa o adaptador principal do Windows somente como alternativa. A implementação cria as entradas específicas de NVIDIA, AMD ou Intel em `AllowList\Gl2Shaders` e na pasta de desempenho do fabricante. Ao desabilitar, os valores existentes antes da ativação são restaurados.
- **Desempenho gráfico aprimorado** controla `Use Performance Pipeline 2020`.
- **Arestas de silhueta por GPU** controla `Use GPU Silhouette Edges` e só pode ser habilitado junto com o Desempenho gráfico aprimorado. Desabilitar o pipeline também desabilita as arestas aceleradas.
- **Antialiasing completo** ativa o antialiasing da cena completa e desativa o modo limitado a arestas sombreadas.

Os ajustes são gravados no perfil do usuário do Windows. Quando o SOLIDWORKS estiver aberto, reinicie-o para que todo o pipeline gráfico seja recarregado.

## Requisitos

- Windows 10/11 64 bits
- .NET Framework 4.8 ou mais recente
- SOLIDWORKS 2025 em português do Brasil ou inglês
- Visual Studio 2022 com **Desenvolvimento para desktop com .NET** apenas para compilar

Não há pacotes NuGet. A interop oficial é carregada diretamente da instalação do SOLIDWORKS e não é copiada para a distribuição.

## Diagnóstico e logs

O EXE cria o log assim que inicia, antes de conectar ao SOLIDWORKS ou tentar atualizar o add-in. O destino principal é:

`%LOCALAPPDATA%\SolidTweaks\Logs\SolidTweaks.log`

Se esse local não puder ser criado, são tentados, nesta ordem:

1. `%TEMP%\SolidTweaks\Logs\SolidTweaks.log`;
2. `SolidTweaks.log` ao lado do executável.

O mesmo arquivo recebe eventos do aplicativo, do processo elevado de registro e da DLL carregada no SOLIDWORKS. Ele inclui hash do EXE, versão, PID, estado dos recursos, GUIDs/caminhos registrados, implantação da DLL, conexão COM, atualização e pilhas completas de exceções. Ao atingir 5 MB, o arquivo anterior é preservado como `SolidTweaks.previous.log`.

O texto de status na parte inferior da janela é um link. Um clique abre diretamente no Explorer a pasta de logs que estiver ativa naquele computador.

## Compilar

1. Abra `SolidTweaks.sln` no Visual Studio 2022.
2. Selecione `Release` e compile a solução.
3. Execute `src\SolidTweaks.App\bin\Release\SolidTweaks.exe`.

Para alterar o atalho, mantenha uma sessão do SOLIDWORKS conectada e um documento ativo.

A compilação `Release` é portátil: a pasta de saída contém somente `SolidTweaks.exe`. O add-in é incorporado ao executável e extraído automaticamente em `%LOCALAPPDATA%\SolidTweaks\Addins` quando um recurso da guia é instalado ou atualizado. O computador de destino ainda precisa ter .NET Framework 4.8 e SOLIDWORKS instalados.

## Estrutura para expansão

- `Core/Features`: contrato persistente de leitura, aplicação e reversão.
- `Core/SolidWorks`: contrato de sessão, documentos e preferências.
- `Features`: uma classe isolada por funcionalidade.
- `Infrastructure/Persistence`: somente estado confirmado da interface.
- `Infrastructure/SolidWorks`: COM, preferências e personalização nativa.
- `Presentation`: formulário, execução assíncrona e status.
- `Shared`: estado persistente compartilhado entre o aplicativo e o add-in.
- `SolidTweaks.SolidWorksAddin`: um único add-in persistente, carregado na inicialização do SOLIDWORKS, com guia dinâmica e comandos no CommandManager.

Os recursos que adicionam botões compartilham a guia `Solid Tweaks`. Ela permanece instalada enquanto ao menos um desses recursos estiver habilitado e só é removida quando todos forem desmarcados. O registro também elimina identidades antigas do add-in para impedir conflitos nas próximas extensões.

Toda linha de recurso que possua um pré-requisito deve usar o indicador visual reutilizável ao lado do ícone de ajuda. O aviso permanece oculto quando o recurso está disponível e, quando bloqueado, informa em vermelho e de forma breve o que falta para habilitá-lo.

Somente durante a inicialização, o formulário calcula sua altura a partir dos cartões, preserva uma margem entre o último cartão e a barra de status e limita o crescimento à área útil do monitor. Novos cartões declarados no painel principal entram automaticamente nesse cálculo inicial. Depois que a janela abre, expandir qualquer botão **+ Opções** nunca altera seu tamanho: o conteúdo adicional usa rolagem vertical e, ao recolher, a rolagem desaparece quando não é mais necessária.

Novos recursos não devem depender do aplicativo Solid Tweaks permanecer aberto. O listener de mouse é carregado e descarregado junto com o add-in do SOLIDWORKS e ignora todas as outras aplicações.
