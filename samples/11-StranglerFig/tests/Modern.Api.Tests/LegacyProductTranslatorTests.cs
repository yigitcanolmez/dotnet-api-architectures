using Modern.Api.AntiCorruption;
using Modern.Api.Domain;

namespace Modern.Api.Tests;

// ACL'in tek sorumluluğu çeviridir; HTTP olmadan saf birim testlerle doğrulanabilir.
public class LegacyProductTranslatorTests
{
    [Fact]
    public void Turkce_bicimli_string_tutar_Money_degerine_cevrilir()
    {
        var ok = LegacyProductTranslator.TryTranslate(
            new LegacyProductDto("33333333-3333-3333-3333-333333333333", "MONITOR", "6.500,00", 5, "A"), out var p);

        Assert.True(ok);
        Assert.Equal(Money.Try(6500m), p.Price);
        Assert.Equal("Monitör", p.Name);
        Assert.True(p.IsSellable);
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("P", false)]
    [InlineData("X9", false)]
    public void STAT_CD_satilabilirlige_cevrilir(string statCd, bool expected)
    {
        LegacyProductTranslator.TryTranslate(new LegacyProductDto(Guid.NewGuid().ToString(), "FAKS CIHAZI", "1.200,00", 1, statCd), out var p);
        Assert.Equal(expected, p.IsSellable);
    }

    [Fact]
    public void Bozuk_legacy_kaydi_reddedilir()
    {
        Assert.False(LegacyProductTranslator.TryTranslate(new LegacyProductDto("not-a-guid", "X", "1,00", 1, "A"), out _));
        Assert.False(LegacyProductTranslator.TryTranslate(new LegacyProductDto(Guid.NewGuid().ToString(), "X", "abc", 1, "A"), out _));
    }
}
