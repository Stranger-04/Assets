"""Recipe-driven gate tests."""
import sys, json, asyncio, os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

async def call(tool_name: str, **kwargs) -> dict:
    from server import server
    result = await server.call_tool(tool_name, kwargs)
    for block in result.content:
        if hasattr(block, 'text'):
            return json.loads(block.text)
    return {"error": "no text"}

async def main():
    print("=" * 50)
    print("  Gate Tests")
    print("=" * 50)

    # ── Production full chain ──
    print("\n── Production (4 gates → write) ──")
    await call("gate_reset")
    await call("gate_set_recipe", name="Production")
    await call("gate_pass", gate_id="g_entry", agent="unity-developer")
    await call("gate_pass", gate_id="g_mode", mode="Production")
    await call("gate_pass", gate_id="g_script", decision="USE scene-query.cs")
    await call("gate_pass", gate_id="g_file", file_type=".shader", category="PostProcess")
    r = await call("write_gated", path="tmp/test_gate.txt", content="ok")
    assert r["status"] == "OK"
    os.remove(os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tmp/test_gate.txt"))
    print(f"  ✅ write_gated OK")

    # ── Quick bypass ──
    print("\n── Quick (0 gates → write) ──")
    await call("gate_reset")
    await call("gate_set_recipe", name="Quick")
    r = await call("write_gated", path="tmp/test_quick.txt", content="quick")
    assert r["status"] == "OK"
    os.remove(os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "tmp/test_quick.txt"))
    print(f"  ✅ Quick: 0 gates, write OK")

    # ── Denied without gates ──
    print("\n── Denied (missing gates) ──")
    await call("gate_reset")
    await call("gate_set_recipe", name="Production")
    r = await call("write_gated", path="tmp/test_deny.txt", content="x")
    assert r["status"] == "DENIED"
    print(f"  ✅ Denied: {r['missing']}")

    # ── Illegal path ──
    print("\n── Illegal path ──")
    await call("gate_reset")
    await call("gate_set_recipe", name="Quick")
    r = await call("write_gated", path="/etc/hosts", content="x")
    assert r["status"] == "DENIED"
    print(f"  ✅ Denied: {r['error']}")

    print("\n" + "=" * 50)
    print("  All tests passed ✓")
    print("=" * 50)

if __name__ == "__main__":
    asyncio.run(main())
