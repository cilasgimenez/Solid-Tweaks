# Solid Tweaks

Aplicativo WinForms leve para SOLIDWORKS 2025 e 2026, desenvolvido em C# e .NET Framework 4.8.

## Versão 1.16.0 — isolamento e reparo de 2025/2026

A regressão do 2025 estava no registro de descoberta e inicialização: a implementação anterior deixava o componente antigo globalmente visível e criava uma entrada duplicada no perfil 2026. O diálogo nativo de suplementos usa também a chave global de startup, portanto a sobreposição por ano não garantia isolamento. A inicialização do 2025 acabou desativada, embora sua DLL e suas preferências de recursos continuassem intactas.

Agora cada identidade é descoberta somente pelo seu ano. A correção move as entradas de descoberta antigas para o 2025, remove as cópias equivocadas no 2026 e preserva os servidores COM antigos. O componente 2025 continua sendo exatamente a DLL 1.14 original, com o mesmo GUID e SHA-256. Quando necessário, seu pacote recebe a TLB e as dependências API 33 ao lado da DLL, sem recompilá-la. O 2026 tem sua própria DLL, TLB e API 34. A TLB é necessária para o carregamento por `LoadAddIn` no host nativo, mesmo quando a ativação COM isolada já funciona.

Ambos usam o mesmo ciclo de ativação: remover o último recurso oculta a interface e desativa o startup, mantendo o objeto carregado dormente. Adicionar novamente reutiliza esse objeto. Atualizar exige que a versão alvo esteja fechada; uma reparação inicial das entradas compartilhadas exige fechar 2025 e 2026. As preferências explícitas são preservadas e o registro é validado contra o pacote completo antes de ser aceito.

Compilação: `dist/SolidTweaks-1.16.0-SW2025-SW2026/SolidTweaks.exe`. Consulte `Validacao.txt` nesse diretório para os testes realizados nas instalações reais, além da suíte automatizada. O reparo nesta máquina já foi aplicado.
## Seleção de SOLIDWORKS

O campo **Versão alvo** identifica as instalações reais no computador pelo Registro e pela versão de `SLDWORKS.exe`. Chaves antigas de instalação sem um executável correspondente não aparecem. O aplicativo suporta 2025 e 2026; outras instalações são identificadas como não suportadas. A última seleção é lembrada. Na primeira execução, 2025 permanece como alvo se estiver instalado.

Cada seleção relê seus checkboxes e atalhos. A conexão COM é conferida pelo número de revisão do servidor; com as duas versões abertas, a pesquisa na Running Object Table encontra a sessão da versão selecionada, e a automação de teclado usa o PID dessa sessão. O seletor fica bloqueado durante operações em andamento.

O perfil do 2025 continua em `HKCU\Software\SolidTweaks\Features`, sem migração automática ao abrir o aplicativo. O perfil do 2026 usa `HKCU\Software\SolidTweaks\Versions\2026\Features`. Habilitar um recurso no 2025 não significa que ele já esteja instalado no 2026; a instalação é feita ao marcar o recurso na versão selecionada. Valores explicitamente desativados são preservados.

**RealView é compartilhado** entre as versões, inclusive o backup de reversão existente. Alterá-lo afeta a lista gráfica comum do SOLIDWORKS, conforme indicado no formulário. Os demais ajustes visuais usam o perfil do ano selecionado.

O executável incorpora a DLL original do 2025, conservada em `src/Compatibility/SW2025`, com o mesmo hash do componente anteriormente instalado. O novo projeto `SolidTweaks.SolidWorks2026Addin` é compilado contra a API oficial instalada do SW2026 (revisão 34), com uma identidade COM própria e estável, definida em `Shared/SolidTweaks2026AddinIdentity.cs`. Essa identidade paralela é necessária para não substituir o servidor COM do 2025. A identidade original em `Shared/SolidTweaksAddinIdentity.cs` permanece inalterada; ela também consta entre as identidades legadas para o perfil 2026, sem ser desregistrada.

A descoberta usa `HKLM\SOFTWARE\SolidWorks\SOLIDWORKS <ano>\Addins\<GUID>`. O startup usa a chave global nativa `HKCU\Software\SolidWorks\AddInsStartup\<GUID>` e a entrada do respectivo ano; cada ano tem um GUID distinto. Nunca se cria uma entrada do componente 2025 no perfil 2026. O componente 2026 também recusa conexões de outro ano antes de criar a interface.
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

Quando necessária, a migração dos antigos estados fixos F/C/M do 2025 acontece somente ao editar explicitamente um atalho configurável. Abrir o aplicativo ou trocar a versão não executa essa migração. O perfil novo do 2026 não migra nem modifica os atalhos do 2025.

### Guia Solid Tweaks

Os checkboxes **Exportar STEP** e **Exportar Chapas DXF** controlam comandos independentes em uma única guia **Solid Tweaks** no CommandManager de peças e montagens.

Os dois checkboxes só ficam habilitados enquanto o aplicativo mantém uma conexão ativa com o SOLIDWORKS. Sem conexão, permanecem bloqueados e exibem o requisito **Necessário abrir o SOLIDWORKS**; um documento aberto não é exigido para instalar ou remover a guia.

- qualquer um dos recursos cria a guia sem depender do outro;
- cada checkbox adiciona ou remove somente seu próprio botão;
- quando o último recurso é removido, a guia desaparece e a inicialização do add-in é desativada; se o SOLIDWORKS estiver aberto, o componente já carregado fica dormente até o encerramento do processo;
- quando os dois estão ativos, os dois botões permanecem na mesma guia;
- o add-in reconstrói somente as guias durante uma alteração, sem depender de unload/reload do componente .NET.

O componente é implantado de forma versionada em `%LOCALAPPDATA%\SolidTweaks\Addins`. Os checkboxes só permanecem marcados depois que o add-in confirma, nas guias de peça e montagem, exatamente os comandos configurados. O registro `HKCU\Software\SolidTweaks\Features` armazena quais comandos devem compor a guia.

A identidade COM da versão 1.14 é permanente para as próximas atualizações normais. Aplicativo e add-in leem o mesmo GUID de `Shared/SolidTweaksAddinIdentity.cs`, e o registrador recusa uma DLL cuja identidade não corresponda. Versões futuras substituem a DLL somente com o SOLIDWORKS fechado; remover e adicionar recursos durante uma sessão apenas reconstrói a interface, sem descarregar e recarregar o componente .NET.

Quando uma versão anterior do add-in está instalada, o aplicativo mostra o botão **Atualizar** no cartão **Recursos** e mantém os checkboxes conforme a configuração já persistida. Após a confirmação do usuário, o SOLIDWORKS é fechado, a nova DLL versionada é instalada e o SOLIDWORKS é aberto novamente. A atualização não força o encerramento do processo se ele não puder ser fechado com segurança e não altera quais comandos estavam habilitados.

### Exportar STEP

O botão **Exportar STEP**, com o ícone `Exportar_Step.png`, usa diretamente a API do SOLIDWORKS, sem macro externa, para:

O botão usa diretamente a API do SOLIDWORKS, sem macro externa, para:

- permitir escolher entre STEP AP203 e AP214, usando AP203 como padrão e preservando a preferência do usuário;
- salvar com geometria sólida e de superfície;
- usar em AP203 e AP214 o perfil de compatibilidade com propriedades de face/aresta, aparências, faces periódicas divididas e componentes de montagem separados;
- criar o arquivo na mesma pasta com o nome `<documento>_HHmmss.STEP`;
- exportar somente corpos visíveis de peças multicorpos;
- exportar somente componentes visíveis de montagens;
- avisar quando o documento ainda não foi salvo;
- oferecer **Exportar** ou **Enviar para Impressão** antes de gerar o arquivo;
- guardar a opção **Abrir pasta** e, quando marcada, abrir a pasta ao concluir a ação **Exportar**;
- ao enviar para impressão, abrir o STEP com o aplicativo padrão associado no Windows;
- restaurar as preferências STEP do usuário depois de cada exportação.

O Windows solicita autorização de administrador quando o add-in precisa ser instalado ou atualizado, pois a descoberta por versão é registrada em `HKLM\SOFTWARE\SolidWorks\SOLIDWORKS <ano>\Addins`. Desmarcar os recursos apenas remove a interface e desativa sua inicialização. O Solid Tweaks pode ser fechado depois da configuração.

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

O bloco **Visual** reúne quatro ajustes persistentes. O estado dos checkboxes é lido diretamente das configurações efetivas. RealView usa a lista gráfica compartilhada; os outros três ajustes usam o registro da versão alvo.

Por padrão, somente **RealView** permanece visível. **+ Opções** expande os outros três ajustes usando a rolagem vertical, sem redimensionar o Form, e **- Opções** recolhe o bloco novamente.

- **RealView** identifica primeiro o renderer que o próprio SOLIDWORKS registrou e usa o adaptador principal do Windows somente como alternativa. A implementação cria as entradas específicas de NVIDIA, AMD ou Intel em `AllowList\Gl2Shaders` e na pasta de desempenho do fabricante. Ao desabilitar, os valores existentes antes da ativação são restaurados.
- **Desempenho gráfico aprimorado** controla `Use Performance Pipeline 2020`.
- **Arestas de silhueta por GPU** controla `Use GPU Silhouette Edges` e só pode ser habilitado junto com o Desempenho gráfico aprimorado. Desabilitar o pipeline também desabilita as arestas aceleradas.
- **Antialiasing completo** ativa o antialiasing da cena completa e desativa o modo limitado a arestas sombreadas.

Os ajustes são gravados no perfil do usuário do Windows. Quando o SOLIDWORKS estiver aberto, reinicie-o para que todo o pipeline gráfico seja recarregado.

## Requisitos

- Windows 10/11 64 bits
- .NET Framework 4.8 ou mais recente
- SOLIDWORKS 2025 ou 2026 em português do Brasil ou inglês
- Visual Studio 2022 com **Desenvolvimento para desktop com .NET** apenas para compilar

Não há pacotes NuGet. Na compilação, as bibliotecas oficiais de interoperabilidade são obtidas das instalações do SOLIDWORKS. O EXE incorpora as dependências da API 33 para o componente 2025 e da API 34 para o componente 2026, extraindo cada conjunto junto à DLL correspondente.

## Diagnóstico e logs

O EXE cria o log assim que inicia, antes de conectar ao SOLIDWORKS ou tentar atualizar o add-in. O destino principal é:

`%LOCALAPPDATA%\SolidTweaks\Logs\SolidTweaks.log`

Se esse local não puder ser criado, são tentados, nesta ordem:

1. `%TEMP%\SolidTweaks\Logs\SolidTweaks.log`;
2. `SolidTweaks.log` ao lado do executável.

O mesmo arquivo recebe eventos do aplicativo, do processo elevado de registro e da DLL carregada no SOLIDWORKS. Ele inclui hash do EXE, versão, PID, estado dos recursos, GUIDs/caminhos registrados, implantação da DLL, conexão COM, atualização e pilhas completas de exceções. Ao atingir 5 MB, o arquivo anterior é preservado como `SolidTweaks.previous.log`.

O texto de status na parte inferior da janela é um link. Um clique abre diretamente no Explorer a pasta de logs que estiver ativa naquele computador.

## Compilar

1. Abra `SolidTweaks.sln` no Visual Studio 2022, com as APIs do SW2025 e SW2026 instaladas.
2. Selecione `Release` e compile a solução, ou execute `powershell -File .\build.ps1`.
3. Execute `dist\SolidTweaks-1.16.0-SW2025-SW2026\SolidTweaks.exe`.

O caminho da API 2026 é detectado no Registro de 64 bits; pode ser informado ao MSBuild por `/p:SolidWorks2026ApiDir=...`. O aplicativo usa o subconjunto da API 2025 para compatibilidade, resolvendo a versão exata das bibliotecas incorporadas ao executável. A distribuição continua portátil, com os dois componentes incorporados ao EXE. O antigo `src\SolidTweaks.App\bin\Release\SolidTweaks.exe` foi preservado; ele não é a nova compilação.

`powershell -File .\build.ps1 -Test` executa os testes automatizados. Eles usam chaves temporárias redirecionadas com `RegOverridePredefKey`, sessões simuladas e renderização do formulário, sem instalar o add-in no SOLIDWORKS real. Cobrem isolamento de preferências, detecção de chaves antigas, correspondência de GUID/API, preservação do pacote 2025, troca de alvo, rolagem e os seis fluxos do ciclo de vida. O pacote extraído do EXE também é carregado em processos novos, sem pré-carregar a API; retirar `swpublished` deve reproduzir a falha e restaurá-la deve permitir o carregamento. Dependências ausentes ou danificadas devem invalidar o pacote. A validação prática de conexão, STEP/DXF, seleção de versão e startup foi executada nos dois SOLIDWORKS instalados. O auxiliar `tests/LiveValidation.csproj` usa o ciclo real da aplicação e a API nativa; suas ações de escrita são executadas somente durante uma validação explícita, fora da suíte isolada. Os documentos de teste ficam em `tests/artifacts/live-validation`.

Referências da API: [identificação de versão e acesso aos add-ins](https://help.solidworks.com/2026/english/api/sldworksapiprogguide/Overview/Accessing_Add-ins.htm) e [registros de add-ins específicos por versão](https://help.solidworks.com/2025/English/api/sldworksapiprogguide/Overview/Add-in_Icons.htm).

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

Os recursos que adicionam botões compartilham a guia `Solid Tweaks`. Ela permanece visível enquanto ao menos um desses recursos estiver habilitado e é removida quando todos forem desmarcados. O componente carregado não é descarregado no meio da sessão, o que permite recriar a guia com segurança; sem recursos ativos, ele não é carregado na próxima inicialização. O registro limita a descoberta de cada componente ao seu ano. Os servidores COM e arquivos antigos do 2025 são preservados.

Toda linha de recurso que possua um pré-requisito deve usar o indicador visual reutilizável ao lado do ícone de ajuda. O aviso permanece oculto quando o recurso está disponível e, quando bloqueado, informa em vermelho e de forma breve o que falta para habilitá-lo.

Somente durante a inicialização, o formulário calcula sua altura a partir dos cartões, preserva uma margem entre o último cartão e a barra de status e limita o crescimento à área útil do monitor. Novos cartões declarados no painel principal entram automaticamente nesse cálculo inicial. Depois que a janela abre, expandir qualquer botão **+ Opções** nunca altera seu tamanho: o conteúdo adicional usa rolagem vertical e, ao recolher, a rolagem desaparece quando não é mais necessária.

Novos recursos não devem depender do aplicativo Solid Tweaks permanecer aberto. O listener de mouse é carregado e descarregado junto com o add-in do SOLIDWORKS e ignora todas as outras aplicações.
