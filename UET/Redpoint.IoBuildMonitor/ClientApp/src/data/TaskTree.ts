export type RunInfo = {
    runId: string;
    firstSeenUtc: number;
}

export type JobInfo = {
    runId: string;
    jobId: string;
    firstSeenUtc: number;
}

export type TaskInfo = {
    runId: string;
    jobId: string;
    taskId: string;
    firstSeenUtc: number;
    messageType: string;
    timestamp: string;
    category: string;
    severity: string;
    stepName: string;
    message: string;
    isSuccess: boolean | null;
    isFilled: boolean | null;
    matchFillRate: number | null;
    teams: {
        slots: {
            type: string;
            userId: string;
        }[];
    }[] | null;
    userId: string | null;
    partyId: string;
    currentStatus: string;
    currentDetail: string;
    currentProgress: number | null;
    etaTimestamp: string;
    criticalErrorMessage: string | null;
    recentLines: string | null;
    warningCount: number;
    errorCount: number;
    restartCount: number;
    durationSeconds: number | null;
    expectChaosKill: boolean;
};

export class SortedDictionary<K, V> implements Iterable<{key: K, value: V}> {
    private _sorter: (key: K, value: V) => number;
    private _map: Map<K, V>;
    private _keys: Array<K>;
    private _writeOp: number;

    constructor(sorter: (key: K, value: V) => number) {
        this._sorter = sorter;
        this._map = new Map<K, V>();
        this._keys = new Array<K>();
        this._writeOp = 0;
    }

    [Symbol.iterator](): Iterator<{ key: K; value: V; }, any, undefined> {
        class SortedDictionaryIterator<K, V> implements Iterator<{key: K, value: V}, any, undefined> {
            private _dict: SortedDictionary<K, V>;
            private _index: number;
            private _writeOpSnapshot: number;
        
            constructor (dict: SortedDictionary<K, V>) {
                this._dict = dict;
                this._index = 0;
                this._writeOpSnapshot = dict._writeOp;
            }
        
            public next(): IteratorResult<{key: K, value: V}> {
                if (this._writeOpSnapshot !== this._dict._writeOp) {
                    throw new Error("SortedDictionary was modified while iterating!");
                }
                if (this._index >= this._dict._keys.length) {
                    return {
                        done: true,
                        value: null,
                    };
                }
                const mapValue = this._dict._map.get(this._dict._keys[this._index]);
                if (mapValue === undefined) {
                    throw new Error("Inconsistent state within SortedDictionary!");
                }
                const result = {
                    done: this._index >= this._dict._keys.length,
                    value: {
                        key: this._dict._keys[this._index],
                        value: mapValue,
                    }
                }
                this._index++;
                return result;
            }
        }
        return new SortedDictionaryIterator(this);
    }

    public setMany(entries: {key: K, value: V}[]): void {
        if (entries.length === 0) {
            return;
        }
        for (const entry of entries) {
            if (this._map.has(entry.key)) {
                this._map.set(entry.key, entry.value);
            } else {
                this._map.set(entry.key, entry.value);
                this._keys.push(entry.key);
            }
        }
        this._keys.sort((a: K, b: K) => {
            const aVal = this._map.get(a);
            if (aVal === undefined) { throw new Error("SortedDictionary state inconsistent!"); }
            const bVal = this._map.get(b);
            if (bVal === undefined) { throw new Error("SortedDictionary state inconsistent!"); }
            const aHash = this._sorter(a, aVal);
            const bHash = this._sorter(b, bVal);
            if (aHash < bHash) {
                return -1;
            } else if (aHash === bHash) {
                return 0;
            } else {
                return 1;
            }
        });
        this._writeOp++;
    }

    public set(key: K, value: V): void {
        if (this._map.has(key)) {
            this._map.set(key, value);
        } else {
            this._map.set(key, value);
            this._keys.push(key);
            this._keys.sort((a: K, b: K) => {
                const aVal = this._map.get(a);
                if (aVal === undefined) { throw new Error("SortedDictionary state inconsistent!"); }
                const bVal = this._map.get(b);
                if (bVal === undefined) { throw new Error("SortedDictionary state inconsistent!"); }
                const aHash = this._sorter(a, aVal);
                const bHash = this._sorter(b, bVal);
                if (aHash < bHash) {
                    return -1;
                } else if (aHash === bHash) {
                    return 0;
                } else {
                    return 1;
                }
            });
        }
        this._writeOp++;
    }

    public has(key: K): boolean {
        return this._map.has(key);
    }

    public get(key: K): V | undefined {
        return this._map.get(key);
    }

    public size(): number {
        return this._keys.length;
    }

    public getMust(key: K): V {
        const value = this._map.get(key);
        if (value === undefined) {
            throw new Error("getMust called on a non-existant key!");
        }
        return value;
    }

    public delete(key: K): void {
        if (!this._map.has(key)) {
            return;
        }

        this._map.delete(key);
        this._keys.splice(this._keys.indexOf(key), 1);
        this._writeOp++;
    }
}

type RunEntry = {
    runInfo: RunInfo,
    jobs: SortedDictionary<string, JobEntry>
};

type JobEntry = {
    jobInfo: JobInfo,
    tasks: SortedDictionary<string, TaskInfo>
};

function runSort(runId: string, runEntry: RunEntry) {
    if (runId === "streaming") {
        return 0;
    }
    return Number.MAX_SAFE_INTEGER - runEntry.runInfo.firstSeenUtc;
}

function jobSort(jobId: string, jobEntry: JobEntry) {
    return Number.MAX_SAFE_INTEGER - jobEntry.jobInfo.firstSeenUtc;
}

function taskSort(taskId: string, taskInfo: TaskInfo) {
    return Number.MAX_SAFE_INTEGER - taskInfo.firstSeenUtc;
}

export class TaskTree implements Iterable<{key: string, value: RunEntry}> {
    private _data: SortedDictionary<string, RunEntry>;
    private _users: Map<string, string>;
    private _parties: Map<string, string>;

    constructor() {
        this._data = new SortedDictionary<string, RunEntry>(runSort);
        this._users = new Map<string, string>();
        this._parties = new Map<string, string>();
    }

    private fqid(runId: string, jobId: string, taskId: string) {
        return runId + ":" + jobId + ":" + taskId;
    }

    [Symbol.iterator](): Iterator<{ key: string; value: RunEntry; }, any, undefined> {
        return this._data[Symbol.iterator]();
    }

    public getUserId(runId: string, jobId: string, taskId: string): string | undefined {
        return this._users.get(this.fqid(runId, jobId, taskId));
    }

    public getPartyId(runId: string, jobId: string, taskId: string): string | undefined {
        return this._parties.get(this.fqid(runId, jobId, taskId));
    }

    public initFromDataSet(runs: RunInfo[], jobs: JobInfo[], tasks: TaskInfo[]) {
        // Initialize runs.
        const runEntries = [];
        for (const run of runs) {
            runEntries.push({
                key: run.runId,
                value: {
                    runInfo: run,
                    jobs: new SortedDictionary<string, JobEntry>(jobSort),
                }
            });
        }
        this._data.setMany(runEntries);

        // Sort jobs by runs.
        jobs.sort((a, b) => {
            if (a.runId < b.runId) {
                return -1;
            } else if (a.runId === b.runId) {
                return 0;
            } else {
                return 1;
            }
        });

        // Initialize jobs.
        let currentRunId = null;
        let jobEntries = [];
        for (const job of jobs) {
            if (currentRunId === null) {
                currentRunId = job.runId;
            }
            if (currentRunId !== job.runId) {
                this._data.getMust(currentRunId).jobs.setMany(jobEntries);
                currentRunId = job.runId;
                jobEntries = [];
            }
            jobEntries.push({
                key: job.jobId,
                value: {
                    jobInfo: job,
                    tasks: new SortedDictionary<string, TaskInfo>(taskSort),
                }
            });
        }
        if (jobEntries.length > 0 && currentRunId !== null) {
            this._data.getMust(currentRunId).jobs.setMany(jobEntries);
            currentRunId = null;
            jobEntries = [];
        }

        // Sort tasks by runs then jobs.
        tasks.sort((a, b) => {
            let aKey = a.runId + ":" + a.jobId;
            let bKey = b.runId + ":" + b.jobId;
            if (aKey < bKey) {
                return -1;
            } else if (aKey === bKey) {
                return 0;
            } else {
                return 1;
            }
        });
        currentRunId = null;
        let currentJobId = null;
        let taskEntries = [];
        for (const task of tasks) {
            if (currentRunId === null) { 
                currentRunId = task.runId;
            }
            if (currentJobId === null) { 
                currentJobId = task.jobId;
            }
            if (currentRunId !== task.runId || 
                currentJobId !== task.jobId) {
                this._data.getMust(currentRunId).jobs.getMust(currentJobId).tasks.setMany(taskEntries);
                currentRunId = task.runId;
                currentJobId = task.jobId;
                taskEntries = [];
            }
            taskEntries.push({
                key: task.taskId,
                value: task,
            });
            const fq = this.fqid(task.runId, task.jobId, task.taskId);
            if (task.userId !== null) {
                this._users.set(fq, task.userId);
            }
            if (task.partyId !== null) {
                this._parties.set(fq, task.partyId);
            }
        }
        if (taskEntries.length > 0 && currentRunId !== null && currentJobId !== null) {
            this._data.getMust(currentRunId).jobs.setMany(jobEntries);
            this._data.getMust(currentRunId).jobs.getMust(currentJobId).tasks.setMany(taskEntries);
            currentRunId = null;
            currentJobId = null;
            taskEntries = [];
        }
    }

    public update(task: TaskInfo) {
        if (!this._data.has(task.runId)) {
            this._data.set(task.runId, {
                runInfo: {
                    runId: task.runId,
                    firstSeenUtc: Date.now() / 1000,
                },
                jobs: new SortedDictionary<string, JobEntry>(jobSort),
            });
        }
        const run = this._data.getMust(task.runId);
        if (!run.jobs.has(task.jobId)) {
            run.jobs.set(task.jobId, {
                jobInfo: {
                    runId: task.runId,
                    jobId: task.jobId,
                    firstSeenUtc: Date.now() / 1000,
                },
                tasks: new SortedDictionary<string, TaskInfo>(taskSort),
            });
        }
        run.jobs.getMust(task.jobId).tasks.set(task.taskId, task);
        const fq = this.fqid(task.runId, task.jobId, task.taskId);
        if (task.userId !== null && !this._users.has(fq)) {
            this._users.set(fq, task.userId);
        }
        if (task.partyId !== null && !this._parties.has(fq)) {
            this._parties.set(fq, task.partyId);
        }
    }

    public remove(runId: string, jobId: string, taskId: string): boolean {
        let mutated = false;
        if (this._data.has(runId)) {
            if (this._data.getMust(runId).jobs.has(jobId)) {
                if (this._data.getMust(runId).jobs.getMust(jobId).tasks.has(taskId)) {
                    this._data.getMust(runId).jobs.getMust(jobId).tasks.delete(taskId);
                    mutated = true;
                }
                if (this._data.getMust(runId).jobs.getMust(jobId).tasks.size() === 0) {
                    this._data.getMust(runId).jobs.delete(jobId);
                    mutated = true;
                }
            }
            if (this._data.getMust(runId).jobs.size() === 0) {
                this._data.delete(runId);
                mutated = true;
            }
        }
        return mutated;
    }

    public removeJob(runId: string, jobId: string): boolean {
        let mutated = false;
        if (this._data.has(runId)) {
            if (this._data.getMust(runId).jobs.has(jobId)) {
                this._data.getMust(runId).jobs.delete(jobId);
                mutated = true;
            }
            if (this._data.getMust(runId).jobs.size() === 0) {
                this._data.delete(runId);
                mutated = true;
            }
        }
        return mutated;
    }

    public removeRun(runId: string): boolean {
        let mutated = false;
        if (this._data.has(runId)) {
            this._data.delete(runId);
            mutated = true;
        }
        return mutated;
    }
}