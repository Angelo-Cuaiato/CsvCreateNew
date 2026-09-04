using System.Collections.Concurrent;

namespace FluxoCaixa.Core.Historico;

/// <summary>
/// Histórico que vive só na memória do processo. É o que sobra quando não há
/// banco configurado - útil para desenvolvimento e teste, e honesto sobre si
/// mesmo: <see cref="Persistente"/> é falso, e a tela avisa.
/// </summary>
public sealed class RepositorioAnalisesEmMemoria : IRepositorioAnalises
{
    private readonly ConcurrentDictionary<Guid, Analise> _analises = new();

    public bool Persistente => false;

    public Task GuardarAsync(Analise analise, CancellationToken cancelamento = default)
    {
        _analises[analise.Id] = analise;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ResumoDeAnalise>> ListarAsync(int limite = 50, CancellationToken cancelamento = default)
    {
        IReadOnlyList<ResumoDeAnalise> lista = _analises.Values
            .OrderByDescending(a => a.EnviadoEm)
            .ThenByDescending(a => a.Id)
            .Take(limite)
            .Select(a => a.Resumo())
            .ToList();

        return Task.FromResult(lista);
    }

    public Task<Analise?> PorIdAsync(Guid id, CancellationToken cancelamento = default)
        => Task.FromResult(_analises.GetValueOrDefault(id));

    public Task<bool> ApagarAsync(Guid id, CancellationToken cancelamento = default)
        => Task.FromResult(_analises.TryRemove(id, out _));
}
