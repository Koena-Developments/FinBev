SELECT r."Id", r."Status", d."Name" as dept, c."UserName"
FROM "CashRequests" r
JOIN "Departments" d ON d."Id" = r."DepartmentId"
JOIN "AspNetUsers" c ON c."Id" = r."ClaimantUserId"
WHERE r."Status" = 1
ORDER BY r."SubmittedAtUtc" DESC
LIMIT 5;
