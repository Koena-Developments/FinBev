SELECT t."Stage", t."Decision", a."UserName" as assigned, a."Id" as assigned_id
FROM "ApprovalTasks" t
JOIN "AspNetUsers" a ON a."Id" = t."AssignedApproverUserId"
WHERE t."CashRequestId" = '67a6fbaa-1555-4ea8-b3cc-9ce86f829041';

SELECT u."UserName", u."Id", d."Name"
FROM "AspNetUsers" u
JOIN "Departments" d ON d."Id" = u."DepartmentId"
WHERE u."UserName" IN ('thabang.motswenyane', 'mpumelelo.qomiso');

SELECT m."PrimaryApproverUserId", p."UserName" as primary_user, m."ActingApproverUserId", act."UserName" as acting_user, d."Name"
FROM "ApprovalMatrices" m
JOIN "Departments" d ON d."Id" = m."DepartmentId"
JOIN "AspNetUsers" p ON p."Id" = m."PrimaryApproverUserId"
LEFT JOIN "AspNetUsers" act ON act."Id" = m."ActingApproverUserId"
WHERE d."Name" = 'ICT';
