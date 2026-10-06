import { onMounted as a, openBlock as l, createElementBlock as c, createVNode as m, unref as n } from "vue";
import p from "@/components/common/datatable/DataTable.vue";
import { usePermissionGroupStore as i } from "@/stores/admin/permissionGroups";
const T = {
  // 主頁面元件
  component: () => Promise.resolve().then(() => b),
  // 網址（需對應後端選單節點）
  path: "/module-template",
  // 標題
  title: "模組範本"
}, d = (r, e) => {
  const o = r.__vccOpts || r;
  for (const [t, s] of e)
    o[t] = s;
  return o;
}, u = { class: "module-template" }, _ = /* @__PURE__ */ Object.assign({
  name: "ModuleTemplate"
}, {
  __name: "ModuleTemplate",
  setup(r) {
    const e = i(), o = [
      { key: "name", label: "名稱" },
      { key: "description", label: "描述" }
    ], t = () => e.fetchPermissionGroups();
    return a(t), (s, g) => (l(), c("div", u, [
      m(p, {
        dataList: n(e).permissionGroups,
        columns: o,
        loading: n(e).loading,
        error: n(e).error,
        "custom-options": { rowKey: "id" },
        onRetry: t
      }, null, 8, ["dataList", "loading", "error"])
    ]));
  }
}), f = /* @__PURE__ */ d(_, [["__scopeId", "data-v-803add9a"]]), b = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  default: f
}, Symbol.toStringTag, { value: "Module" }));
export {
  T as default
};
