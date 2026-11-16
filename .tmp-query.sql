SELECT u."UserName", d."Name" AS dept
FROM "AspNetUsers" u
JOIN "Departments" d ON d."Id" = u."DepartmentId"
WHERE u."UserName" IN ('thabang.motswenyane', 'mpumelelo.qomiso');

SELECT r."Id", r."Status", r."Reason", d."Name" AS dept, c."UserName" AS claimant, r."SubmittedAtUtc"
FROM "CashRequests" r
JOIN "Departments" d ON d."Id" = r."DepartmentId"
JOIN "AspNetUsers" c ON c."Id" = r."ClaimantUserId"
ORDER BY r."CreatedAtUtc" DESC
LIMIT 8;

SELECT t."Stage", t."Decision", a."UserName" AS assigned, r."Status"
FROM "ApprovalTasks" t
JOIN "AspNetUsers" a ON a."Id" = t."AssignedApproverUserId"
JOIN "CashRequests" r ON r."Id" = t."CashRequestId"
ORDER BY t."Id" DESC
LIMIT 8;
