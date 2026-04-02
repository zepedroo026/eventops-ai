# EventOps AI — Gestão Operacional e Logística de Recursos para Eventos

Plataforma web tipo mini-ERP para gestão operacional e logística de eventos, desenvolvida no âmbito do Laboratório de Projeto em Engenharia Informática (LPEI) da Licenciatura em Engenharia Informática — UTAD.

## Sobre o Projeto

O EventOps AI foca-se na componente **Back-of-House** de eventos, permitindo à equipa organizadora planear e gerir recursos internos como salas, staff, fornecedores e cronograma técnico (Run of Show).

### Funcionalidades principais
- Gestão de eventos e salas
- Run of Show com timeline interativa
- Deteção automática de conflitos de horário
- Gestão de staff e alocação de tarefas
- Controlo orçamental e registo de despesas
- Autenticação com controlo de acessos por perfil

## Stack Tecnológica

| Componente | Tecnologia |
|---|---|
| Backend | ASP.NET Core (.NET) |
| Base de Dados | PostgreSQL (Supabase) |
| Autenticação | JWT |
| Deploy Backend | Render (free tier) |
| Deploy Frontend | Vercel (free tier) |

## Estrutura do Repositório
```
eventops-ai/
├── src/
│   ├── EventOps.API/          # Projeto ASP.NET Core (API REST)
│   ├── EventOps.Core/         # Modelos e lógica de negócio
│   └── EventOps.Infrastructure/ # Acesso à base de dados
├── frontend/                  # Interface web
├── docs/                      # Documentação e diagramas UML
└── tests/                     # Testes unitários e de integração
```

## Informação Académica

- **Aluno:** José Pedro Dias Granja (81452)
- **Orientador:** Frederico Branco
- **Coorientadores:** Pedro Couto e Emanuel Peres
- **Ano letivo:** 2025/2026
