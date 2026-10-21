// SPDX-License-Identifier: GPL-2.0-or-later
// uc_host - a plain native host that runs am4ever's DXE drivers under Unicorn against a
// simulated ACPI EC. Built as a non-CET exe so Unicorn's longjmp-based TCG can execute
// (a .NET host has CET user shadow stack on, which fail-fasts). Ports the Ec/Dxe classes
// of G:\HPBIOS\tools\emulate_modules.py. See native/README.md.
#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>
#include <unicorn/unicorn.h>

#define IMAGE_BASE 0x10000000ULL
#define STACK_BASE 0x20000000ULL
#define SYS_BASE   0x30000000ULL
#define RET_ADDR   0x30008000ULL

typedef struct {
    int present;
    uint8_t regs[256];
    int touched[256];
    int have_cmd, cmd, have_addr, addr, have_obf, obf;   // EC port state machine
    uint32_t cf8, lpc44;
    long insns;
} Sim;

static uint32_t ec_inb(Sim *s, uint32_t port) {
    if (!s->present) return 0xFF;
    if (port == 0x66) return s->have_obf ? 1 : 0;        // OBF bit0; IBF bit1 always 0 (instant EC)
    if (port == 0x62) { uint32_t v = s->have_obf ? (uint32_t)s->obf : 0; s->have_obf = 0; return v; }
    return 0xFF;
}

static void ec_outb(Sim *s, uint32_t port, uint32_t v) {
    if (!s->present) return;
    if (port == 0x66) { s->cmd = v; s->have_cmd = 1; s->have_addr = 2; return; } // state='addr'
    if (port == 0x62) {
        if (s->have_addr == 2) {                          // expecting address byte
            s->addr = v; s->touched[v & 0xFF] = 1;
            if (s->cmd == 0x80) { s->obf = s->regs[v & 0xFF]; s->have_obf = 1; s->have_addr = 0; }
            else if (s->cmd == 0x81) s->have_addr = 1;     // state='data'
        } else if (s->have_addr == 1 && s->cmd == 0x81) {  // data byte of a write
            s->regs[s->addr & 0xFF] = (uint8_t)v; s->touched[s->addr & 0xFF] = 1; s->have_addr = 0;
        }
    }
}

static uint32_t hook_in(uc_engine *uc, uint32_t port, int size, void *user) {
    (void)uc; (void)size; Sim *s = user;
    if (port == 0xCFC && s->cf8 == 0x8000A344) return s->lpc44;
    if (port == 0x62 || port == 0x66) return ec_inb(s, port);
    return 0xFFFFFFFF;
}

static void hook_out(uc_engine *uc, uint32_t port, int size, uint32_t value, void *user) {
    (void)uc; (void)size; Sim *s = user;
    if (port == 0xCF8) s->cf8 = value;
    else if (port == 0xCFC && s->cf8 == 0x8000A344) s->lpc44 = value;
    else if (port == 0x62 || port == 0x66) ec_outb(s, port, value & 0xFF);
}

static void hook_code(uc_engine *uc, uint64_t a, uint32_t sz, void *user) { (void)uc; (void)a; (void)sz; ((Sim *)user)->insns++; }

static uint32_t rd32(const uint8_t *p) { return p[0] | (p[1] << 8) | (p[2] << 16) | ((uint32_t)p[3] << 24); }

static int run_ecfan(const uint8_t *pe, size_t pe_len, Sim *s, uint64_t limit) {
    if (pe_len < 0x40) { fprintf(stderr, "pe too small\n"); return -1; }
    uint32_t pe_off = rd32(pe + 0x3C);
    if ((uint64_t)pe_off + 24 + 64 > pe_len) { fprintf(stderr, "bad PE header\n"); return -1; }  // 64-bit: no wrap on a huge e_lfanew
    uint16_t nsec = pe[pe_off + 6] | (pe[pe_off + 7] << 8);
    uint16_t opt = pe[pe_off + 20] | (pe[pe_off + 21] << 8);
    uint32_t entry = rd32(pe + pe_off + 24 + 16);
    uint32_t size_img = rd32(pe + pe_off + 24 + 56);

    uc_engine *uc;
    if (uc_open(UC_ARCH_X86, UC_MODE_64, &uc) != UC_ERR_OK) { fprintf(stderr, "uc_open failed\n"); return -1; }
    uc_mem_map(uc, IMAGE_BASE, (size_img + 0xFFFF) & ~0xFFFFULL, UC_PROT_ALL);
    uc_mem_write(uc, IMAGE_BASE, pe, pe_len < 0x400 ? pe_len : 0x400);
    for (uint16_t i = 0; i < nsec; i++) {
        const uint8_t *sh = pe + pe_off + 24 + opt + 40 * i;
        if ((size_t)(sh - pe) + 40 > pe_len) break;
        uint32_t vs = rd32(sh + 8), va = rd32(sh + 12), rs = rd32(sh + 16), ro = rd32(sh + 20);
        uint32_t n = (vs && vs < rs) ? vs : rs;
        if ((size_t)ro + n <= pe_len) uc_mem_write(uc, IMAGE_BASE + va, pe + ro, n);
    }
    uc_mem_map(uc, STACK_BASE, 0x100000, UC_PROT_ALL);
    uc_mem_map(uc, SYS_BASE, 0x10000, UC_PROT_ALL);
    uint8_t hlt = 0xF4; uc_mem_write(uc, RET_ADDR, &hlt, 1);

    uc_hook h1, h2, h3;
    uc_hook_add(uc, &h1, UC_HOOK_INSN, (void *)hook_in, s, 1, 0, UC_X86_INS_IN);
    uc_hook_add(uc, &h2, UC_HOOK_INSN, (void *)hook_out, s, 1, 0, UC_X86_INS_OUT);
    uc_hook_add(uc, &h3, UC_HOOK_CODE, (void *)hook_code, s, 1, 0);

    uint64_t sp = STACK_BASE + 0xFF000, ret = RET_ADDR;
    uc_mem_write(uc, sp, &ret, 8);
    uc_reg_write(uc, UC_X86_REG_RSP, &sp);
    uint64_t rcx = 0x40000000, rdx = SYS_BASE;
    uc_reg_write(uc, UC_X86_REG_RCX, &rcx);
    uc_reg_write(uc, UC_X86_REG_RDX, &rdx);

    uc_err e = uc_emu_start(uc, IMAGE_BASE + entry, RET_ADDR, 0, limit);
    uint64_t rip = 0; uc_reg_read(uc, UC_X86_REG_RIP, &rip);
    int status = (e != UC_ERR_OK) ? 2 : (rip != RET_ADDR ? 1 : 0);
    uc_close(uc);
    return status;
}

// ==========================================================================
// PEI: run a 32-bit AMD CPM OEM PEIM with fake PEI services under one CPU family.
// Ports the Pei class + run_pei of emulate_modules.py.
// ==========================================================================
#define P_STUB      0x00F00000u
#define P_TABLE     0x00F10000u
#define P_PTRS      0x00F11000u
#define P_IDT       0x00F12000u
#define P_HEAP      0x01000000u
#define P_STACKTOP  0x00E00000u
#define P_RETMAGIC  0x00F0FFF0u
#define P_PPISTUB   0x00F08000u
#define P_PPIOBJ    0x00F13000u
#define NSVC 27
enum { SVC_InstallPpi = 0, SVC_LocatePpi = 2, SVC_NotifyPpi = 3, SVC_GetBootMode = 4, SVC_AllocatePool = 13, SVC_CopyMem = 14, SVC_SetMem = 15 };

typedef struct { uint8_t guid[16]; uint32_t ptr, flags; } Ppi;
typedef struct { uint8_t guid[16]; uint32_t fn, flags, desc; } Notify;
typedef struct {
    uc_engine *uc; uint32_t base, img_lo, img_hi, heap, cpuid;
    Ppi installed[64]; int n_inst;
    Notify notifies[64]; int n_notify;
    int n_located; long writes_outside; int bad;   // bad = unmapped/stray flagged
} Pei;

static uint32_t p_rd32(uc_engine *uc, uint32_t a) { uint32_t v = 0; uc_mem_read(uc, a, &v, 4); return v; }
static uint32_t p_arg(Pei *p, int i) { uint32_t esp = 0; uc_reg_read(p->uc, UC_X86_REG_ESP, &esp); return p_rd32(p->uc, esp + 4 + 4 * i); }

static void p_svc(uc_engine *uc, uint64_t addr, uint32_t sz, void *user) {
    (void)sz; Pei *p = user; int idx = (int)((addr - P_STUB) / 0x10); uint32_t status = 0;
    if (idx == SVC_LocatePpi) {
        p->n_located++;
        uint8_t g[16]; uc_mem_read(uc, p_arg(p, 1), g, 16);
        static const uint8_t pcd[16] = {0x58,0x1c,0xe8,0x06,0xd7,0x4a,0xbc,0x44,0x83,0x90,0xf1,0x02,0x65,0xf7,0x24,0x80};
        if (memcmp(g, pcd, 16) == 0) { uint32_t obj = P_PPIOBJ; uc_mem_write(uc, p_arg(p, 4), &obj, 4); }
        else status = 0x8000000E;
    } else if (idx == SVC_InstallPpi || idx == SVC_NotifyPpi) {
        uint32_t d = p_arg(p, 1);
        for (;;) {
            uint32_t flags = p_rd32(uc, d), gp = p_rd32(uc, d + 4), ptr = p_rd32(uc, d + 8);
            if (idx == SVC_InstallPpi) { if (p->n_inst < 64) { uc_mem_read(uc, gp, p->installed[p->n_inst].guid, 16); p->installed[p->n_inst].ptr = ptr; p->installed[p->n_inst].flags = flags; p->n_inst++; } }
            else { if (p->n_notify < 64) { uc_mem_read(uc, gp, p->notifies[p->n_notify].guid, 16); p->notifies[p->n_notify].fn = ptr; p->notifies[p->n_notify].flags = flags; p->notifies[p->n_notify].desc = d; p->n_notify++; } }
            if (flags & 0x80000000u) break; d += 12;
        }
    } else if (idx == SVC_AllocatePool) {
        uint32_t n = p_arg(p, 1), a = p->heap; p->heap += (n + 15) & ~15u;
        uint8_t zero = 0; for (uint32_t i = 0; i < n; i++) uc_mem_write(uc, a + i, &zero, 1);
        uc_mem_write(uc, p_arg(p, 2), &a, 4);
    } else if (idx == SVC_GetBootMode) { uint32_t z = 0; uc_mem_write(uc, p_arg(p, 1), &z, 4); }
    else if (idx == SVC_CopyMem) { uint32_t dst = p_arg(p, 0), src = p_arg(p, 1), n = p_arg(p, 2); uint8_t b; for (uint32_t i = 0; i < n; i++) { uc_mem_read(uc, src + i, &b, 1); uc_mem_write(uc, dst + i, &b, 1); } }
    else if (idx == SVC_SetMem) { uint32_t dst = p_arg(p, 0), n = p_arg(p, 1); uint8_t v = (uint8_t)p_arg(p, 2); for (uint32_t i = 0; i < n; i++) uc_mem_write(uc, dst + i, &v, 1); }
    uc_reg_write(uc, UC_X86_REG_EAX, &status);
}

static void p_ppicall(uc_engine *uc, uint64_t a, uint32_t s, void *u) { (void)a; (void)s; (void)u; uint32_t z = 0; uc_reg_write(uc, UC_X86_REG_EAX, &z); }
static uint32_t p_cpuid_in(uc_engine *uc, uint32_t port, int size, void *u) { (void)uc; (void)port; (void)size; (void)u; return 0xFFFFFFFF; }
static void p_out(uc_engine *uc, uint32_t port, int size, uint32_t v, void *u) { (void)uc; (void)port; (void)size; (void)v; (void)u; }
static int p_cpuid(uc_engine *uc, void *user) {   // returns 1 to skip the built-in CPUID and use our values
    Pei *p = user; uint32_t leaf = 0; uc_reg_read(uc, UC_X86_REG_EAX, &leaf);
    uint32_t eax = 0, ebx = 0, ecx = 0, edx = 0;
    if (leaf == 1 || leaf == 0x80000001) { eax = p->cpuid; if (leaf == 0x80000001) ebx = 0x20000000; }
    else if (leaf == 0) { eax = 0xD; ebx = 0x68747541; ecx = 0x444D4163; edx = 0x69746E65; }
    uc_reg_write(uc, UC_X86_REG_EAX, &eax); uc_reg_write(uc, UC_X86_REG_EBX, &ebx);
    uc_reg_write(uc, UC_X86_REG_ECX, &ecx); uc_reg_write(uc, UC_X86_REG_EDX, &edx);
    return 1;
}
static void p_wr(uc_engine *uc, uc_mem_type t, uint64_t addr, int size, int64_t val, void *user) {
    (void)t; (void)size; (void)val; Pei *p = user;
    int ok = (addr >= P_STACKTOP - 0x40000 && addr < P_STACKTOP) || (addr >= P_HEAP && addr < P_HEAP + 0x100000) || (addr >= p->img_lo && addr < p->img_hi + 0x2000);
    if (!ok) p->writes_outside++;
}
static bool p_unmapped(uc_engine *uc, uc_mem_type t, uint64_t addr, int size, int64_t val, void *user) { (void)uc; (void)t; (void)addr; (void)size; (void)val; ((Pei *)user)->bad = 1; return false; }

static uint32_t p_call(Pei *p, uint32_t fn, uint32_t a0, uint32_t a1, int have_ecx, uint32_t ecx, uint64_t limit, int *returned) {
    uint32_t sp = P_STACKTOP - 0x100; uint32_t frame[3] = { P_RETMAGIC, a0, a1 };
    sp -= 12; uc_mem_write(p->uc, sp, frame, 12); uc_reg_write(p->uc, UC_X86_REG_ESP, &sp);
    if (have_ecx) uc_reg_write(p->uc, UC_X86_REG_ECX, &ecx);
    uc_err e = uc_emu_start(p->uc, fn, P_RETMAGIC, 0, limit);
    uint32_t eip = 0; uc_reg_read(p->uc, UC_X86_REG_EIP, &eip);
    *returned = (e == UC_ERR_OK && eip == P_RETMAGIC);
    uint32_t eax = 0; uc_reg_read(p->uc, UC_X86_REG_EAX, &eax); return eax;
}

// Walk a CPM OEM table pointer list; returns tag count, fills tags (4 chars + optional /pmask) into out.
static int p_walk_tables(Pei *p, uint32_t lst, char out[64][24]) {
    int n = 0;
    for (int i = 0; i < 64; i++) {
        uint32_t t = p_rd32(p->uc, lst + 4 * i); if (t == 0) break;
        uint8_t h[16]; uc_mem_read(p->uc, t, h, 16);
        int cpm = (h[0] == '$' && h[1] == 'C' && h[2] == 'P' && h[3] == 'M');
        int axx = (h[0] == '$' && h[1] == 'A');
        if (!cpm && !axx) { snprintf(out[n++], 24, "BAD"); continue; }
        uint32_t pmask; memcpy(&pmask, h + 8, 4);
        if (pmask) snprintf(out[n], 24, "%.4s/0x%X", h, pmask); else snprintf(out[n], 24, "%.4s", h);
        n++;
    }
    return n;
}

static int run_pei(const uint8_t *te, size_t te_len, uint32_t cpuid_eax, int have_override, uint32_t override_off) {
    if (te_len < 40 || te[0] != 'V' || te[1] != 'Z') { fprintf(stderr, "not a TE image\n"); return -1; }
    uint16_t mach; memcpy(&mach, te + 2, 2); if (mach != 0x14C) { fprintf(stderr, "not i386 TE\n"); return -1; }
    uint16_t stripped; uint32_t entry, imagebase_lo; uint32_t codebase; uint64_t imagebase;
    memcpy(&stripped, te + 6, 2); memcpy(&entry, te + 8, 4); memcpy(&codebase, te + 12, 4); memcpy(&imagebase, te + 16, 8);
    (void)imagebase_lo; (void)codebase;
    Pei p; memset(&p, 0, sizeof(p)); p.cpuid = cpuid_eax;
    p.base = (uint32_t)imagebase + stripped - 40; uint32_t entry_addr = (uint32_t)imagebase + entry;
    if (uc_open(UC_ARCH_X86, UC_MODE_32, &p.uc) != UC_ERR_OK) { fprintf(stderr, "uc_open failed\n"); return -1; }
    uc_engine *uc = p.uc;
    uint32_t lo = p.base & ~0xFFFu;
    uc_mem_map(uc, lo, ((p.base + te_len + 0x2000 - lo) + 0xFFF) & ~0xFFFu, UC_PROT_ALL);
    uc_mem_write(uc, p.base, te, te_len);
    p.img_lo = p.base; p.img_hi = (uint32_t)(p.base + te_len);
    uc_mem_map(uc, P_STACKTOP - 0x40000, 0x40000, UC_PROT_ALL);
    uc_mem_map(uc, P_STUB, 0x20000, UC_PROT_ALL);
    uc_mem_map(uc, P_HEAP, 0x100000, UC_PROT_ALL); p.heap = P_HEAP;
    // PEI services table: "PEI SERV" magic then NSVC stub pointers
    { uint8_t tbl[0x18 + 4 * NSVC]; memset(tbl, 0, sizeof(tbl));
      uint64_t magic = 0x5652455320494550ULL; memcpy(tbl, &magic, 8);
      for (int i = 0; i < NSVC; i++) { uint32_t s = P_STUB + 0x10 * i; memcpy(tbl + 0x18 + 4 * i, &s, 4); }
      uc_mem_write(uc, P_TABLE, tbl, sizeof(tbl)); }
    uint32_t t_table = P_TABLE; uc_mem_write(uc, P_PTRS, &t_table, 4);
    uint32_t t_ptrs = P_PTRS; uc_mem_write(uc, P_IDT - 4, &t_ptrs, 4);
    uc_x86_mmr idtr = { 0, P_IDT, 0x7FF, 0 }; uc_reg_write(uc, UC_X86_REG_IDTR, &idtr);
    { uint8_t ret = 0xC3; for (int i = 0; i < 0x1000; i++) uc_mem_write(uc, P_STUB + i, &ret, 1);
      for (int i = 0; i < 0x4000; i++) uc_mem_write(uc, P_PPISTUB + i, &ret, 1);
      uint8_t hlt = 0xF4; uc_mem_write(uc, P_RETMAGIC, &hlt, 1); }
    // fake PCD PPI object: a table of method stub pointers
    uc_mem_map(uc, P_PPIOBJ & ~0xFFFu, 0x2000, UC_PROT_ALL);
    for (int i = 0; i < 21; i++) { uint32_t m = P_PPISTUB + 0x10 * i; uc_mem_write(uc, P_PPIOBJ + 4 * i, &m, 4); }

    uc_hook h[7];
    uc_hook_add(uc, &h[0], UC_HOOK_CODE, (void *)p_svc, &p, P_STUB, P_STUB + 0x10 * NSVC);
    uc_hook_add(uc, &h[1], UC_HOOK_CODE, (void *)p_ppicall, &p, P_PPISTUB, P_PPISTUB + 0x10 * 0x400);
    uc_hook_add(uc, &h[2], UC_HOOK_INSN, (void *)p_cpuid, &p, 1, 0, UC_X86_INS_CPUID);
    uc_hook_add(uc, &h[3], UC_HOOK_INSN, (void *)p_cpuid_in, &p, 1, 0, UC_X86_INS_IN);
    uc_hook_add(uc, &h[4], UC_HOOK_INSN, (void *)p_out, &p, 1, 0, UC_X86_INS_OUT);
    uc_hook_add(uc, &h[5], UC_HOOK_MEM_WRITE, (void *)p_wr, &p, 1, 0);
    uc_hook_add(uc, &h[6], UC_HOOK_MEM_UNMAPPED, (void *)p_unmapped, &p, 1, 0);

    int returned = 0;
    uint32_t r = p_call(&p, entry_addr, 0x1000, P_PTRS, 0, 0, 2000000, &returned);
    if (returned && r == 0x80000003) { printf("entry=0x80000003\ntables=\nwrites_outside=0\n"); uc_close(uc); return 0; }  // EFI_UNSUPPORTED

    // fire notify callbacks once each (a PPI is installed or faked)
    for (int i = 0; i < p.n_notify; i++) { int rr; p_call(&p, p.notifies[i].fn, P_PTRS, p.notifies[i].desc, 0, 0, 2000000, &rr); }

    // find the CPM OEM table list among the installed PPIs
    char tags[64][24]; int ntags = 0;
    for (int i = 0; i < p.n_inst && ntags == 0; i++) {
        if (!p.installed[i].ptr) continue;
        for (int off = 0; off <= 8 && ntags == 0; off += 4) {
            uint32_t lst = p_rd32(uc, p.installed[i].ptr + off);
            char cand[64][24]; int n = p_walk_tables(&p, lst, cand);
            int bad = 0; for (int k = 0; k < n; k++) if (strcmp(cand[k], "BAD") == 0) bad = 1;
            if (n >= 4 && !bad) { ntags = n; memcpy(tags, cand, sizeof(cand)); }
        }
    }

    printf("entry=0x%X\n", r);
    printf("ppis=%d\nnotifies=%d\n", p.n_inst, p.n_notify);
    printf("tables=");
    for (int i = 0; i < ntags; i++) printf("%s%s", i ? " " : "", tags[i]);
    printf("\nwrites_outside=%ld\n", p.writes_outside + (p.bad ? 1 : 0));
    if (have_override) {
        // The neutralised routine is xor eax,eax; ret — call it (ecx=PeiServices**) and confirm it
        // returns 0 with no new stray writes and no LocatePpi, i.e. a genuine no-op.
        long w0 = p.writes_outside; int l0 = p.n_located;
        int rr; uint32_t ov = p_call(&p, p.base + override_off, 0, 0, 1, P_PTRS, 10, &rr);
        int clean = rr && ov == 0 && p.writes_outside == w0 && p.n_located == l0;
        printf("override[0x%X]=%s\n", override_off, clean ? "ok" : "live");
    }
    uc_close(uc);
    return 0;
}

static int pei_main(int argc, char **argv) {
    const char *te_path = NULL; uint32_t cpuid = 0, ov = 0; int have_ov = 0;
    for (int i = 2; i < argc; i++) {
        if (!strcmp(argv[i], "--te") && i + 1 < argc) te_path = argv[++i];
        else if (!strcmp(argv[i], "--cpuid") && i + 1 < argc) cpuid = (uint32_t)strtoul(argv[++i], NULL, 0);
        else if (!strcmp(argv[i], "--override") && i + 1 < argc) { ov = (uint32_t)strtoul(argv[++i], NULL, 0); have_ov = 1; }
    }
    if (!te_path) { fprintf(stderr, "--te required\n"); return 64; }
    FILE *f = fopen(te_path, "rb"); if (!f) { fprintf(stderr, "cannot open %s\n", te_path); return 64; }
    fseek(f, 0, SEEK_END); long n = ftell(f); fseek(f, 0, SEEK_SET);
    uint8_t *te = malloc(n); if (!te || fread(te, 1, n, f) != (size_t)n) { fclose(f); free(te); return 64; } fclose(f);
    int rc = run_pei(te, n, cpuid, have_ov, ov); free(te);
    return rc < 0 ? 1 : 0;
}

static int selftest(void) {
    uc_engine *uc;
    if (uc_open(UC_ARCH_X86, UC_MODE_32, &uc) != UC_ERR_OK) return 2;
    uc_mem_map(uc, 0x1000, 0x1000, UC_PROT_ALL);
    uint8_t code[] = {0x41, 0x41, 0x41};
    uc_mem_write(uc, 0x1000, code, sizeof(code));
    int64_t ecx = 0; uc_reg_write(uc, UC_X86_REG_ECX, &ecx);
    uc_err e = uc_emu_start(uc, 0x1000, 0x1003, 0, 0);
    uc_reg_read(uc, UC_X86_REG_ECX, &ecx);
    uc_close(uc);
    if (e == UC_ERR_OK && ecx == 3) { printf("selftest=ok\n"); return 0; }
    printf("selftest=fail\n"); return 2;
}

int main(int argc, char **argv) {
    if (argc >= 2 && strcmp(argv[1], "selftest") == 0) return selftest();
    if (argc >= 2 && strcmp(argv[1], "pei") == 0) return pei_main(argc, argv);
    if (argc < 2 || strcmp(argv[1], "ecfan") != 0) { fprintf(stderr, "usage: uc_host ecfan --pe <file> [--no-ec] [--reg h=h] | pei --te <file> --cpuid <hex> [--override <hex>] | selftest\n"); return 64; }

    Sim s; memset(&s, 0, sizeof(s)); s.present = 1;
    s.regs[0x7E] = 0x00; s.regs[0x80] = 0x03;             // defaults from emulate_modules.py
    const char *pe_path = NULL; uint64_t limit = 50000000;
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i], "--pe") == 0 && i + 1 < argc) pe_path = argv[++i];
        else if (strcmp(argv[i], "--no-ec") == 0) s.present = 0;
        else if (strcmp(argv[i], "--reg") == 0 && i + 1 < argc) {
            unsigned a, v; if (sscanf(argv[++i], "%x=%x", &a, &v) == 2) s.regs[a & 0xFF] = (uint8_t)v;
        }
    }
    if (!pe_path) { fprintf(stderr, "--pe required\n"); return 64; }
    FILE *f = fopen(pe_path, "rb");
    if (!f) { fprintf(stderr, "cannot open %s\n", pe_path); return 64; }
    fseek(f, 0, SEEK_END); long n = ftell(f); fseek(f, 0, SEEK_SET);
    uint8_t *pe = malloc(n); if (fread(pe, 1, n, f) != (size_t)n) { fclose(f); return 64; } fclose(f);

    int status = run_ecfan(pe, n, &s, limit);
    free(pe);
    if (status < 0) return 1;
    printf("status=%d\n", status);
    printf("insns=%ld\n", s.insns);
    printf("lpc=0x%X\n", s.lpc44);
    for (int i = 0; i < 256; i++) if (s.touched[i]) printf("ec[0x%X]=0x%X\n", i, s.regs[i]);
    return 0;
}
