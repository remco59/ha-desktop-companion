"""Config flow for Desktop Companion."""

from __future__ import annotations

import voluptuous as vol

from homeassistant import config_entries
from homeassistant.const import CONF_NAME

from .const import DOMAIN


class DesktopCompanionConfigFlow(config_entries.ConfigFlow, domain=DOMAIN):
    """Handle a config flow for Desktop Companion."""

    VERSION = 1

    async def async_step_user(self, user_input=None):
        """Ask for a friendly name for this Windows device."""
        if user_input is not None:
            return self.async_create_entry(
                title=user_input[CONF_NAME],
                data={CONF_NAME: user_input[CONF_NAME]},
            )

        return self.async_show_form(
            step_id="user",
            data_schema=vol.Schema(
                {vol.Required(CONF_NAME, default="Windows PC"): str}
            ),
        )
