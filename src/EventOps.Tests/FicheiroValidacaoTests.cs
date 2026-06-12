using EventOps.Core.Models;

namespace EventOps.Tests;

/// <summary>
/// Testes de validação de ficheiros de fornecedor (extensão e tamanho).
/// A lógica está em FornecedoresController — testamos as regras de negócio
/// via helpers estáticos equivalentes.
/// </summary>
public class FicheiroValidacaoTests
{
    private static readonly string[] AllowedExts = [".pdf", ".png", ".jpg", ".jpeg", ".svg"];
    private const long MaxBytes = 10 * 1024 * 1024;

    private static string? ValidateFicheiro(string nomeOriginal, long tamanhoBytes)
    {
        var ext = Path.GetExtension(nomeOriginal).ToLowerInvariant();
        if (!AllowedExts.Contains(ext))
            return $"Extensão não permitida. Aceites: {string.Join(", ", AllowedExts)}";
        if (tamanhoBytes > MaxBytes)
            return "O ficheiro não pode ter mais de 10 MB.";
        return null;
    }

    // ── Extensões permitidas ─────────────────────────────────────────────────

    [Theory]
    [InlineData("relatorio.pdf")]
    [InlineData("logo.PNG")]
    [InlineData("foto.jpg")]
    [InlineData("foto.JPEG")]
    [InlineData("icone.svg")]
    public void Extensao_Valida_NaoDeveRetornarErro(string nome)
    {
        var erro = ValidateFicheiro(nome, 1024);
        Assert.Null(erro);
    }

    // ── Extensões não permitidas ─────────────────────────────────────────────

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("script.js")]
    [InlineData("doc.docx")]
    [InlineData("planilha.xlsx")]
    [InlineData("arquivo.zip")]
    public void Extensao_NaoPermitida_DeveRetornarErro(string nome)
    {
        var erro = ValidateFicheiro(nome, 1024);
        Assert.NotNull(erro);
        Assert.Contains("Extensão não permitida", erro);
    }

    // ── Tamanho máximo ───────────────────────────────────────────────────────

    [Fact]
    public void Tamanho_Exato10MB_DevePassar()
    {
        var erro = ValidateFicheiro("ficheiro.pdf", MaxBytes);
        Assert.Null(erro);
    }

    [Fact]
    public void Tamanho_Acima10MB_DeveRetornarErro()
    {
        var erro = ValidateFicheiro("ficheiro.pdf", MaxBytes + 1);
        Assert.NotNull(erro);
        Assert.Contains("10 MB", erro);
    }

    [Fact]
    public void Tamanho_Zero_DevePassar()
    {
        var erro = ValidateFicheiro("empty.pdf", 0);
        Assert.Null(erro);
    }

    // ── Combinação: extensão errada E tamanho excedido → extensão primeiro ──

    [Fact]
    public void ExtensaoErroneaComTamanhoGrande_DeveRetornarErroDeExtensao()
    {
        var erro = ValidateFicheiro("virus.exe", MaxBytes + 1000);
        Assert.NotNull(erro);
        Assert.Contains("Extensão não permitida", erro);
    }

    // ── Case-insensitive ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("FOTO.JPG")]
    [InlineData("LOGO.SVG")]
    [InlineData("RELATORIO.PDF")]
    public void Extensao_Maiusculas_DevePassar(string nome)
    {
        var erro = ValidateFicheiro(nome, 512);
        Assert.Null(erro);
    }

    // ── Modelo: TipoFicheiro enum completo ───────────────────────────────────

    [Fact]
    public void TipoFicheiro_EnumTodosOsValores_SaoDefinidos()
    {
        var valores = Enum.GetValues<TipoFicheiro>();
        Assert.Contains(TipoFicheiro.Fatura,          valores);
        Assert.Contains(TipoFicheiro.MaterialGrafico, valores);
        Assert.Contains(TipoFicheiro.Outro,           valores);
    }
}
