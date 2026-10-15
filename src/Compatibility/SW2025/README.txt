Componente original da versão 1.14 preservado para SOLIDWORKS 2025.

SHA-256:
7F65297E4C3D396BC3CFDBC7332CDEB4C44F8BF147295A659822C6D981531426

O executável incorpora este binário sem recompilá-lo. Seu hash, GUID e caminho
COM correspondem ao componente original já instalado nesta máquina.

A TLB foi exportada desse mesmo binário com TlbExp do SDK .NET Framework 4.8.
As dependências API 33 são incorporadas a partir da instalação do SW2025.
O pacote completo permite carga nativa por LoadAddIn após reiniciar desativado.

O componente 2026 é compilado separadamente contra API 34. A descoberta dos
suplementos usa a chave de cada ano; não usar uma entrada global do componente
2025 junto de uma sobreposição no 2026, pois o startup nativo também é global.
Não substituir a DLL 2025 por uma recompilação do código compartilhado.