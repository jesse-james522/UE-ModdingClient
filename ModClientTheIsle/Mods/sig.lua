local patch = function(ctx, address)
	ctx[address] = 0xC3
end

local readint = function(ctx, i)
	if (ctx[i+3] >> 7) == 1 then
		local val = ctx[i] | (ctx[i+1] << 8) | (ctx[i+2] << 16) | (ctx[i+3] << 24)
		return val - 0x100000000
	else
		return ctx[i] | (ctx[i+1] << 8) | (ctx[i+2] << 16) | (ctx[i+3] << 24)
	end
end

local followjump = function(ctx, address)
	if ctx[address] == 0xE9 then
		return address + readint(ctx, address + 1) + 5
	else
		print(string.format("Wrong jump instruction offset: 0x%X", address))
		return 0
	end
end

local universalpatch = function(ctx)
	local firstjump = followjump(ctx, ctx:address() + 0x37)
	if firstjump ~= 0 then
		patch(ctx, firstjump)
	end
end

return {
  { match = universalpatch, pattern = '48 8D 0D ?? ?? ?? ?? E9 ?? ?? ?? ?? CC CC CC CC 48 83 EC 28 E8 ?? ?? ?? ?? 48 89 05 ?? ?? ?? ?? 48 83 C4 28 C3 CC CC CC CC CC CC CC CC CC CC CC 48 8D 0D ?? ?? ?? ?? E9 ?? ?? ?? ?? CC CC CC CC 48 8D 0D ?? ?? ?? ?? E9 ?? ?? ?? ?? CC CC CC CC'} -- universal
}
