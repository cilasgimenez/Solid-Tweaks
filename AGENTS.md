# Instruções do projeto Solid Tweaks

## Marcos e backups

- Quando o usuário solicitar um novo **Marco**, compacte a pasta completa do projeto `D:\PROJETOS\CODEX\Solid Tweaks` em um arquivo ZIP.
- Salve o ZIP em `D:\PROJETOS\CODEX\Solid Tweaks - Backups`.
- Use obrigatoriamente o nome `SolidTweaks-v1.x.zip`.
- Se o usuário informar a versão do marco, use exatamente a versão indicada.
- Se a versão não for informada, examine os arquivos `SolidTweaks-v1.x.zip` existentes, encontre o maior valor inteiro de `x` e incremente-o em 1. Exemplo: depois de `SolidTweaks-v1.8.zip`, crie `SolidTweaks-v1.9.zip`.
- Não sobrescreva silenciosamente um marco existente.
- Depois da criação, verifique se o ZIP pode ser aberto e se contém a pasta completa do projeto.

## Layout do formulário

- Sempre preserve um espaço visível entre o último bloco do formulário e a barra de status.
- A altura vertical do formulário deve ser calculada somente na inicialização, considerando todos os blocos principais declarados no painel.
- Na inicialização, evite a rolagem vertical sempre que o conteúdo couber na área útil do monitor; quando não couber, limite o formulário à tela e mantenha a margem inferior acessível ao final da rolagem.
- Depois que a aplicação estiver aberta, botões **+ Opções** nunca devem alterar o tamanho do Form. Expansões devem preservar a altura atual e usar rolagem vertical; ao recolher, remova a rolagem se o conteúdo voltar a caber.
- Todo novo bloco adicionado ao painel principal deve participar do cálculo inicial de altura.

## Ciclo de vida do add-in do SOLIDWORKS

- A identidade COM definida em `src/Shared/SolidTweaksAddinIdentity.cs` é estável. Não altere o GUID em atualizações normais de versão; versões novas devem trocar somente a DLL, a versão e o caminho de implantação.
- Se uma nova identidade COM for inevitável, mova primeiro a identidade atual para `LegacyGuids` e mantenha aplicativo e add-in consumindo a mesma definição compartilhada.
- Nunca descarregue e tente recarregar a mesma identidade do add-in durante uma sessão aberta do SOLIDWORKS. Ao remover o último recurso, remova a interface, desative a inicialização e deixe o componente carregado dormente até o SOLIDWORKS encerrar.
- Atualizações que substituem a DLL do add-in devem ocorrer somente depois que o SOLIDWORKS for fechado e devem validar a correspondência entre o GUID esperado pelo aplicativo e o GUID contido na DLL antes de alterar o registro.
- Valores de recursos explicitamente persistidos como desativados nunca podem ser interpretados como estado ausente por rotinas de migração.
- Antes de entregar mudanças nesse ciclo, valide estes fluxos: remover os dois botões; adicionar novamente na mesma sessão; reiniciar com ambos desativados; adicionar após reiniciar; atualizar com ambos desativados; atualizar com um ou ambos os recursos ativos.
