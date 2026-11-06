namespace Modern.Api.Domain;

// [Domain] Modern sistemin ürün kavramı. Ürünler HENÜZ taşınmadı; asıl kaynak legacy sistem.
// Bu tip legacy'nin PRD_NM / PRC / STAT_CD alanlarından habersizdir — çeviriyi ACL yapar.
public sealed record Product(Guid Id, string Name, Money Price, bool IsSellable);
