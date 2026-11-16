UPDATE "AspNetUsers" u
SET "DepartmentId" = d."Id"
FROM "Departments" d
WHERE d."Name" = 'ICT' AND u."UserName" = 'thabang.motswenyane';

SELECT u."UserName", d."Name" AS dept
FROM "AspNetUsers" u
JOIN "Departments" d ON d."Id" = u."DepartmentId"
WHERE u."UserName" = 'thabang.motswenyane';
