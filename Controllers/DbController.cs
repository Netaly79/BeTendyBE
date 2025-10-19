﻿namespace BeTendyBE.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using BeTendyBE.Data;

    [ApiController]
    [Route("db")]
    public class DbController: ControllerBase
    {
        private readonly AppDbContext _db;
        public DbController(AppDbContext db) => _db = db;

        [HttpGet("ok")]
        public async Task<IActionResult> OkAsync()
        {
            var canConnect = await _db.Database.CanConnectAsync();
            return Ok(new { db = canConnect ? "connected" : "failed" });
        }
    }
}
