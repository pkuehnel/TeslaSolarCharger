﻿using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers
{
    public class ConfigController : ApiBaseController
    {
        private readonly IConfigService _service;
        private readonly ITeslaFleetApiService _teslaFleetApiService;

        public ConfigController(IConfigService service, ITeslaFleetApiService teslaFleetApiService)
        {
            _service = service;
            _teslaFleetApiService = teslaFleetApiService;
        }

        /// <summary>
        /// Get all settings and status of all cars
        /// </summary>
        [HttpGet]
        public ISettings GetSettings() => _service.GetSettings();

        /// <summary>
        /// Update Car's configuration
        /// </summary>
        /// <param name="carId">Car Id of car to update</param>
        /// <param name="carConfiguration">Car Configuration which should be set to car</param>
        [HttpPut]
        public Task UpdateCarConfiguration(int carId, [FromBody] CarConfiguration carConfiguration) =>
            _service.UpdateCarConfiguration(carId, carConfiguration);

        /// <summary>
        /// Get basic Configuration of cars, which are not often changed
        /// </summary>
        [HttpGet]
        public Task<List<CarBasicConfiguration>> GetCarBasicConfigurations() => _service.GetCarBasicConfigurations();

        /// <summary>
        /// Update Car's configuration
        /// </summary>
        /// <param name="carId">Car Id of car to update</param>
        /// <param name="carBasicConfiguration">Car Configuration which should be set to car</param>
        [HttpPut]
        public Task UpdateCarBasicConfiguration(int carId, [FromBody] CarBasicConfiguration carBasicConfiguration) =>
            _service.UpdateCarBasicConfiguration(carId, carBasicConfiguration);

        [HttpPost]
        public Task AddTeslaFleetApiToken([FromBody]DtoTeslaFleetApiRefreshToken token, TeslaFleetApiRegion region) =>
            _teslaFleetApiService.AddNewTokenAsync(token, region);
    }
}
