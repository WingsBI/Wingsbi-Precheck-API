namespace Precheck.Repository.Queries
{
    public static class AnalyticsQueries
    {
        // One round trip, aggregates only. Each branch yields (Metric, Total) rows.
        //  - PO status mirrors the precheck status calculation used by the production order list
        //    (1 = Pending, 2 = Partial, 3 = Completed; status 4 "Pending-Planner" is not reported).
        //  - QR status ids: 1 = Ready for consumption, 3 = Generated, 2 = Consumed.
        //  - Rejected components: active precheck rows flagged isrejected = 1.
        //  - Pending swap: rejected components with no active swap recorded for the same
        //    drawing number + id number.
        public static readonly string GET_ANALYTICS_SUMMARY = @"
WITH PoStatus AS (
    SELECT
        pom.id,
        CASE
            WHEN pom.min IS NULL OR LTRIM(RTRIM(pom.min)) = '' THEN 4
            WHEN COUNT(ppd.id) > 0
                 AND COUNT(ppd.id) = SUM(CASE WHEN ppd.isprecheckcomplete = 1 THEN 1 ELSE 0 END) THEN 3
            WHEN SUM(CASE WHEN ppd.isprecheckcomplete = 1 THEN 1 ELSE 0 END) > 0 THEN 2
            ELSE 1
        END AS StatusId
    FROM tbl_productionordermaster pom
    LEFT JOIN tbl_projectdetails pd
        ON pom.id = pd.productionordernumberid AND pd.isactive = 1
    LEFT JOIN tbl_projectprecheckdetails ppd
        ON pd.id = ppd.projectdetailsid AND ppd.isactive = 1
    WHERE pom.isactive = 1
    GROUP BY pom.id, pom.min
)
SELECT
    CASE StatusId WHEN 1 THEN 'po_pending' WHEN 2 THEN 'po_partial' ELSE 'po_completed' END AS Metric,
    COUNT(*) AS Total
FROM PoStatus
WHERE StatusId IN (1, 2, 3)
GROUP BY StatusId

UNION ALL

SELECT
    CASE qrcodestatusid WHEN 1 THEN 'qr_ready' WHEN 2 THEN 'qr_consumed' ELSE 'qr_generated' END,
    COUNT(*)
FROM tbl_qrcodedetails
WHERE qrcodestatusid IN (1, 2, 3)
GROUP BY qrcodestatusid

UNION ALL

SELECT 'mr_rejected', COUNT(*)
FROM tbl_projectprecheckdetails
WHERE isactive = 1 AND isrejected = 1

UNION ALL

SELECT 'swap_pending', COUNT(*)
FROM tbl_projectprecheckdetails ppd
WHERE ppd.isactive = 1 AND ppd.isrejected = 1
  AND NOT EXISTS (
      SELECT 1 FROM tbl_swappingdetails sd
      WHERE sd.isActive = 1
        AND sd.swappedDrawingNumberID = ppd.drawingnumberid
        AND sd.fromSwappedIdNumber = ppd.idnumber
  );";
    }
}
