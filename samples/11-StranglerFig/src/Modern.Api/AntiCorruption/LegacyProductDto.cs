namespace Modern.Api.AntiCorruption;

// [ACL] Legacy sistemin "dili". Bu tip SADECE AntiCorruption klasöründe yaşar;
// Domain ve Features bu alan adlarını (PRD_NM, PRC, STAT_CD) asla görmez.
internal sealed record LegacyProductDto(string PRD_ID, string PRD_NM, string PRC, int STK_QTY, string STAT_CD);
