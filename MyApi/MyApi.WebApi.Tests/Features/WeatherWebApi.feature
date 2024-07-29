Feature: WeatherWebApi

Web API for weather forecasts

Scenario: Get weather forecasts
	When I make a GET request to 'weatherforecast'
	Then the response status code is '200'
